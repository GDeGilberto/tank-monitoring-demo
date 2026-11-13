using System.Globalization;

namespace Application.Protocol
{
    /// <summary>Contenido de una trama valida: encabezado de 6 caracteres y cuerpo (desde la fecha).</summary>
    public sealed record TankEnvelope(string Header, string Body);

    public static class TankFrame
    {
        public const char Soh = '\u0001';
        public const char Etx = '\u0003';
        public const string Terminator = "&&";
        public const string UnknownCommandMarker = "ERR0";
        public const int HeaderLength = 6;

        /// <summary>
        /// Valida SOH, ETX, "&amp;&amp;" y checksum ANTES de leer cualquier campo.
        /// Lanza <see cref="TankFrameException"/> con el error tipado.
        /// </summary>
        public static TankEnvelope Unwrap(string? raw)
        {
            if (string.IsNullOrEmpty(raw))
                throw new TankFrameException(TankFrameError.Empty, "La trama esta vacia.");
            if (raw[0] != Soh)
                throw new TankFrameException(TankFrameError.MissingSoh, "La trama no empieza con SOH.");
            if (raw[^1] != Etx)
                throw new TankFrameException(TankFrameError.MissingEtx, "La trama no termina con ETX (incompleta).");

            string inner = raw[1..^1];

            if (inner.StartsWith(UnknownCommandMarker, StringComparison.Ordinal))
            {
                if (inner.Length != UnknownCommandMarker.Length + 4)
                    throw new TankFrameException(TankFrameError.BadChecksumLength, "El checksum de ERR0 no tiene 4 caracteres.");
                string expected = TankChecksum.Compute(raw.AsSpan(0, 1 + UnknownCommandMarker.Length));
                if (!string.Equals(expected, inner[UnknownCommandMarker.Length..], StringComparison.OrdinalIgnoreCase))
                    throw new TankFrameException(TankFrameError.ChecksumMismatch, "El checksum de ERR0 no coincide.");
                throw new TankFrameException(TankFrameError.UnknownCommand, "El lector respondio ERR0: comando no soportado.");
            }

            int terminatorIndex = inner.LastIndexOf(Terminator, StringComparison.Ordinal);
            if (terminatorIndex < 0)
                throw new TankFrameException(TankFrameError.MissingTerminator, "La trama no contiene el terminador de datos.");

            string checksum = inner[(terminatorIndex + Terminator.Length)..];
            if (checksum.Length != 4 || !checksum.All(Uri.IsHexDigit))
                throw new TankFrameException(TankFrameError.BadChecksumLength, "El checksum debe tener exactamente 4 caracteres hexadecimales.");

            // raw = SOH + inner + ETX; el checksum cubre SOH hasta "&&" inclusive.
            string computed = TankChecksum.Compute(raw.AsSpan(0, 1 + terminatorIndex + Terminator.Length));
            if (!string.Equals(computed, checksum, StringComparison.OrdinalIgnoreCase))
                throw new TankFrameException(TankFrameError.ChecksumMismatch, "El checksum de la trama no coincide.");

            if (terminatorIndex < HeaderLength + 10)
                throw new TankFrameException(TankFrameError.Truncated, "La trama es demasiado corta.");

            return new TankEnvelope(inner[..HeaderLength], inner[HeaderLength..terminatorIndex]);
        }

        /// <summary>Lector secuencial con limites estrictos sobre el cuerpo de la trama.</summary>
        internal sealed class Cursor(string body)
        {
            private int _index;
            public bool AtEnd => _index >= body.Length;

            public string Take(int count)
            {
                if (count < 0 || _index + count > body.Length)
                    throw new TankFrameException(TankFrameError.Truncated, "La trama esta truncada.");
                string s = body.Substring(_index, count);
                _index += count;
                return s;
            }

            public char TakeChar() => Take(1)[0];

            public int TakeDecimal(int digits)
            {
                string s = Take(digits);
                if (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
                    throw new TankFrameException(TankFrameError.InvalidField, $"Campo decimal invalido: '{s}'.");
                return value;
            }

            public int TakeHex(int digits)
            {
                string s = Take(digits);
                if (!int.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int value))
                    throw new TankFrameException(TankFrameError.InvalidField, $"Campo hexadecimal invalido: '{s}'.");
                return value;
            }

            public DateTime TakeDate()
            {
                string s = Take(10);
                if (!DateTime.TryParseExact(s, "yyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                    throw new TankFrameException(TankFrameError.InvalidDate, $"Fecha invalida: '{s}'.");
                return date;
            }

            public float TakeFloat()
            {
                string s = Take(8);
                if (!uint.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint bits))
                    throw new TankFrameException(TankFrameError.InvalidFloat, $"Float invalido: '{s}'.");
                float value = BitConverter.Int32BitsToSingle(unchecked((int)bits));
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new TankFrameException(TankFrameError.InvalidFloat, "El valor no es un numero finito.");
                return value;
            }
        }
    }
}
