using Application.Protocol;
using Domain.Entities;

namespace Application.UseCases
{
    /// <summary>
    /// Lee una trama de inventario (INV). Valida el checksum antes de leer campos y rechaza
    /// tramas truncadas, fechas imposibles y floats no finitos con <see cref="TankFrameException"/>.
    /// </summary>
    public class ParseTankInventoryReport
    {
        public TankReport Execute(string input)
        {
            TankEnvelope envelope = TankFrame.Unwrap(input);
            var cursor = new TankFrame.Cursor(envelope.Body);

            DateTime fecha = cursor.TakeDate();
            List<Tank> tanks = [];

            while (!cursor.AtEnd)
            {
                int numeroTanque = cursor.TakeDecimal(2);
                char codigoProducto = cursor.TakeChar();
                int estado = cursor.TakeHex(4);
                int numeroCampos = cursor.TakeHex(2);

                var data = new TankData();
                if (numeroCampos >= 1) data.Volume = cursor.TakeFloat();
                if (numeroCampos >= 2) data.TCVolume = cursor.TakeFloat();
                if (numeroCampos >= 3) data.Ullage = cursor.TakeFloat();
                if (numeroCampos >= 4) data.Height = cursor.TakeFloat();
                if (numeroCampos >= 5) data.Water = cursor.TakeFloat();
                if (numeroCampos >= 6) data.Temperature = cursor.TakeFloat();
                if (numeroCampos >= 7) data.WaterVolume = cursor.TakeFloat();
                // Campos adicionales desconocidos se leen y se descartan.
                for (int i = 7; i < numeroCampos; i++) cursor.TakeFloat();

                tanks.Add(new Tank
                {
                    NoTank = numeroTanque,
                    CodeProduct = codigoProducto,
                    StatusTank = estado,
                    NumberDataHex = numeroCampos,
                    TankData = data,
                });
            }

            return new TankReport
            {
                Date = fecha,
                Tanks = tanks,
                Checksum = input[^5..^1],
            };
        }
    }
}
