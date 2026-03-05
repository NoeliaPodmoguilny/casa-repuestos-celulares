namespace CasaRepuestos.Models
{
    public class ArticuloParaCompra
    {
        public int IdArticulo { get; set; }
        public string NombreArticulo { get; set; }
        public double Precio { get; set; }
        public int IdProveedor { get; set; }
        public string NombreProveedor { get; set; }

        // Propiedad para mostrar en la lista desplegable
        public string DisplayMember
        {
            get { return $"{NombreArticulo} - {NombreProveedor}"; }
        }
    }
}
