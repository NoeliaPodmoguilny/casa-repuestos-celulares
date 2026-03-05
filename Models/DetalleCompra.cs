namespace CasaRepuestos.Models
{
    public class DetalleCompra
    {
        public int IdDetalleCompra { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public int IdCompra { get; set; }
        public int IdArticulo { get; set; }

        public Articulo? Articulo { get; set; }
    }

}
