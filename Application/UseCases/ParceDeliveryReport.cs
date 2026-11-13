using Application.Protocol;
using Domain.Entities;

namespace Application.UseCases
{
    /// <summary>
    /// Lee una trama de entregas (ENT). Valida el checksum antes de leer campos y devuelve
    /// TODAS las entregas de cada tanque.
    /// </summary>
    public class ParceDeliveryReport
    {
        public DeliveryTankReport Execute(string input)
        {
            TankEnvelope envelope = TankFrame.Unwrap(input);
            var cursor = new TankFrame.Cursor(envelope.Body);

            DateTime fecha = cursor.TakeDate();
            List<DeliveryTank> tanks = [];

            while (!cursor.AtEnd)
            {
                int numeroTanque = cursor.TakeDecimal(2);
                char codigoProducto = cursor.TakeChar();
                int conteo = cursor.TakeDecimal(2);

                var deliveries = new List<FullDeliveries>(conteo);
                for (int i = 0; i < conteo; i++)
                    deliveries.Add(ReadDelivery(cursor));

                tanks.Add(new DeliveryTank
                {
                    NoTank = numeroTanque,
                    CodeProduct = codigoProducto,
                    CountDeliveries = conteo,
                    Deliveries = deliveries,
                });
            }

            return new DeliveryTankReport { Date = fecha, Tanks = tanks };
        }

        private static FullDeliveries ReadDelivery(TankFrame.Cursor cursor)
        {
            var delivery = new FullDeliveries
            {
                Start = new DeliveryTankData { Date = cursor.TakeDate() },
                End = new DeliveryTankData { Date = cursor.TakeDate() },
            };

            int campos = cursor.TakeHex(2);
            for (int field = 1; field <= campos; field++)
            {
                float value = cursor.TakeFloat();
                switch (field)
                {
                    case 1: delivery.Start.Volume = value; break;
                    case 2: delivery.Start.TCVolume = value; break;
                    case 3: delivery.Start.Water = value; break;
                    case 4: delivery.Start.Temperature = value; break;
                    case 5: delivery.End.Volume = value; break;
                    case 6: delivery.End.TCVolume = value; break;
                    case 7: delivery.End.Water = value; break;
                    case 8: delivery.End.Temperature = value; break;
                    case 9: delivery.Start.Height = value; break;
                    case 10: delivery.End.Height = value; break;
                }
            }

            return delivery;
        }
    }
}
