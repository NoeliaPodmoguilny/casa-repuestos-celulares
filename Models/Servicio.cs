namespace CasaRepuestos.Models
{
    public class Servicio
    {
        public int IdServicio { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public decimal Precio { get; set; }

        public int? IdArticuloAsociado { get; set; }

        public Articulo? ArticuloAsociado { get; set; }
    }
}