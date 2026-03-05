namespace CasaRepuestos.Models
{
    public class DetalleFacturaAMostrar
    {
        public int Numero_Factura { get; set; }
        public int? Articulo { get; set; }
        public int? Servicio { get; set; }
        // Campos auxiliares para la visualización del DataGridViewFacturasPendientes
        public string Descripcion { get; set; }
        public decimal PrecioUnitario { get; set; }

        public int Cantidad { get; set; }
        public decimal Subtotal { get; set; }



    }
}
