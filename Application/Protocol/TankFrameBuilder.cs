using System.Globalization;
using System.Text;
using Domain.Entities;

namespace Application.Protocol
{
    /// <summary>
    /// Arma tramas del protocolo propio. Lo usan el simulador y las pruebas; es la inversa
    /// de los parsers INV y ENT.
    /// </summary>
    public static class TankFrameBuilder
    {
        public const string InventoryCommand = "INV000";
        public const string DeliveriesCommand = "ENT000";

        /// <summary>Trama de inventario: encabezado de 6 caracteres (p. ej. INV000), fecha y un bloque por tanque.</summary>
        public static string BuildInventory(string header, TankReport report)
        {
            var sb = new StringBuilder();
            sb.Append(TankFrame.Soh).Append(header);
            sb.Append(FormatDate(report.Date));

            foreach (Tank tank in report.Tanks ?? [])
            {
                TankData d = tank.TankData ?? new TankData();
                sb.Append(tank.NoTank.ToString("D2", CultureInfo.InvariantCulture));
                sb.Append(tank.CodeProduct);
                sb.Append(tank.StatusTank.ToString("X4", CultureInfo.InvariantCulture));
                sb.Append(7.ToString("X2", CultureInfo.InvariantCulture));
                AppendFloat(sb, d.Volume);
                AppendFloat(sb, d.TCVolume);
                AppendFloat(sb, d.Ullage ?? 0f);
                AppendFloat(sb, d.Height);
                AppendFloat(sb, d.Water);
                AppendFloat(sb, d.Temperature);
                AppendFloat(sb, d.WaterVolume ?? 0f);
            }

            return Finish(sb);
        }

        /// <summary>Trama de entregas: por tanque, TT + tipo + dd (decimal) y cada entrega con 10 campos.</summary>
        public static string BuildDeliveries(string header, DeliveryTankReport report)
        {
            var sb = new StringBuilder();
            sb.Append(TankFrame.Soh).Append(header);
            sb.Append(FormatDate(report.Date));

            foreach (DeliveryTank tank in report.Tanks ?? [])
            {
                sb.Append(tank.NoTank.ToString("D2", CultureInfo.InvariantCulture));
                sb.Append(tank.CodeProduct);
                sb.Append(tank.Deliveries.Count.ToString("D2", CultureInfo.InvariantCulture));

                foreach (FullDeliveries delivery in tank.Deliveries)
                {
                    sb.Append(FormatDate(delivery.Start.Date));
                    sb.Append(FormatDate(delivery.End.Date));
                    sb.Append(10.ToString("X2", CultureInfo.InvariantCulture));
                    AppendFloat(sb, delivery.Start.Volume);
                    AppendFloat(sb, delivery.Start.TCVolume);
                    AppendFloat(sb, delivery.Start.Water);
                    AppendFloat(sb, delivery.Start.Temperature);
                    AppendFloat(sb, delivery.End.Volume);
                    AppendFloat(sb, delivery.End.TCVolume);
                    AppendFloat(sb, delivery.End.Water);
                    AppendFloat(sb, delivery.End.Temperature);
                    AppendFloat(sb, delivery.Start.Height);
                    AppendFloat(sb, delivery.End.Height);
                }
            }

            return Finish(sb);
        }

        /// <summary>Respuesta a un comando no soportado: SOH + ERR0 + checksum + ETX (sin terminador de datos).</summary>
        public static string BuildUnknownCommand()
        {
            string body = TankFrame.Soh + TankFrame.UnknownCommandMarker;
            return body + TankChecksum.Compute(body) + TankFrame.Etx;
        }

        private static string FormatDate(DateTime date) =>
            date.ToString("yyMMddHHmm", CultureInfo.InvariantCulture);

        private static void AppendFloat(StringBuilder sb, float value)
        {
            int bits = BitConverter.SingleToInt32Bits(value);
            sb.Append(bits.ToString("X8", CultureInfo.InvariantCulture));
        }

        private static string Finish(StringBuilder sb)
        {
            sb.Append(TankFrame.Terminator);
            string checksum = TankChecksum.Compute(sb.ToString());
            sb.Append(checksum).Append(TankFrame.Etx);
            return sb.ToString();
        }
    }
}
