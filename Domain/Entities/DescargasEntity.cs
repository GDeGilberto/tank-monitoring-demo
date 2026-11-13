namespace Domain.Entities
{
    public class DescargasEntity
    {
        public int Id { get; }
        public int IdSitio { get; }
        public int NoTanque { get; }
        public double VolumenInicial { get; }
        public double? TemperaturaInicial { get; }
        public DateTime FechaInicial { get; }
        public double VolumenDisponible { get; }
        public double? TemperaturaFinal { get; }
        public DateTime FechaFinal { get; }
        public double CantidadCargada { get; }

        public DescargasEntity(int idSitio, int noTanque, double volumenInicial, 
            double? temperaturaInicial, DateTime fechaInicial, double volumenDisponible, 
            double? temperaturaFinal, DateTime fechaFinal, double cantidadCargada)
        {
            IdSitio = idSitio;
            NoTanque = noTanque;
            VolumenInicial = volumenInicial;
            TemperaturaInicial = temperaturaInicial;
            FechaInicial = fechaInicial;
            VolumenDisponible = volumenDisponible;
            TemperaturaFinal = temperaturaFinal;
            FechaFinal = fechaFinal;
            CantidadCargada = cantidadCargada;
        }

        public DescargasEntity(int id ,int idSitio, int noTanque, double volumenInicial, 
            double? temperaturaInicial, DateTime fechaInicial, double volumenDisponible, 
            double? temperaturaFinal, DateTime fechaFinal, double cantidadCargada)
        {
            Id = id;
            IdSitio = idSitio;
            NoTanque = noTanque;
            VolumenInicial = volumenInicial;
            TemperaturaInicial = temperaturaInicial;
            FechaInicial = fechaInicial;
            VolumenDisponible = volumenDisponible;
            TemperaturaFinal = temperaturaFinal;
            FechaFinal = fechaFinal;
            CantidadCargada = cantidadCargada;
        }
    }

}
