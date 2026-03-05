namespace CasaRepuestos.Models
{
    public class Compra
    {
        public int IdCompra { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
        public int IdProveedor { get; set; }

        public string Tipo { get; set; } = "VENTA";
 

        public int? IdOrdenCompra { get; set; }

        public Proveedor? Proveedor { get; set; }
        public string MetodoPago { get; set; }
        public List<DetalleCompra> Detalles { get; set; } = new();
    }

}
