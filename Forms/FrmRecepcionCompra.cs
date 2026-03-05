using CasaRepuestos.Models;
using CasaRepuestos.Services;
using System.Globalization;

namespace CasaRepuestos.Forms
{
    public partial class FrmRecepcionCompra : Form
    {
        private readonly int _idOrdenCompra;
        private readonly ComprasService _comprasService = new ComprasService();
        private OrdenCompra _ordenActual;

        public FrmRecepcionCompra(int idOrdenCompra)
        {
            InitializeComponent();
            _idOrdenCompra = idOrdenCompra;
            // Suscribir el evento de edición de celda
            dgvDetallesRecepcion.CellEndEdit += dgvDetallesRecepcion_CellEndEdit;
            CargarDatosOrden();
            // Configurar el ComboBox de métodos de pago
            cmbMetodoPago.Items.AddRange(new object[] {
                "EFECTIVO",
                "TRANSFERENCIA",
                "TARJETA",
                "BILLETERA VIRTUAL",
                "CUENTA CORRIENTE"
            });
            // Seleccionar el primer método por defecto
            cmbMetodoPago.SelectedIndex = 1;
        }

        private void CargarDatosOrden()
        {
            _ordenActual = _comprasService.GetOrdenConDetalles(_idOrdenCompra);
            if (_ordenActual == null)
            {
                MessageBox.Show("Error: No se pudo cargar la orden de compra.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return;
            }
            // Mostrar datos de la orden en los controles correspondientes
            lblNumeroOrden.Text = _ordenActual.IdOrdenCompra.ToString();
            lblProveedor.Text = _ordenActual.ProveedorNombre;
            lblFechaEmision.Text = _ordenActual.FechaCreacion.ToShortDateString();
            // Cargar los detalles en el DataGridView
            dgvDetallesRecepcion.Rows.Clear();
            foreach (var detalle in _ordenActual.Detalles)
            {
                // Usamos la cantidad solicitada como cantidad por defecto a recibir
                decimal subtotal = detalle.CantidadSolicitada * detalle.PrecioUnitarioEstimado;

                dgvDetallesRecepcion.Rows.Add(
                    detalle.IdArticulo,
                    detalle.ArticuloNombre,
                    detalle.CantidadSolicitada, 
                    detalle.PrecioUnitarioEstimado, 
                    subtotal  
                );
            }
            CalcularTotal();
        }

        // Metodo manejador del evento CellEndEdit para validar y recalcular valores
        private void dgvDetallesRecepcion_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            DataGridViewRow row = dgvDetallesRecepcion.Rows[e.RowIndex];
            CultureInfo culture = CultureInfo.GetCultureInfo("es-AR");

            // Validaciones y recalculos después de editar una celda
            if (!int.TryParse(row.Cells["CantidadRecibida"].Value?.ToString(), out int cantidad) || cantidad < 0)
            {
                MessageBox.Show("Ingrese una Cantidad Recibida válida (número entero >= 0).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                row.Cells["CantidadRecibida"].Value = 0;
                cantidad = 0;
            }

            // Validar Precio Unitario Final 
            string precioText = row.Cells["PrecioFinal"].Value?.ToString();
            if (!decimal.TryParse(precioText, NumberStyles.Currency | NumberStyles.Number, culture, out decimal precio) || precio < 0)
            {
                MessageBox.Show("Ingrese un Precio Unitario Final válido.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                precio = 0m;
                row.Cells["PrecioFinal"].Value = precio.ToString("C2", culture);
            }

            //  Recalcular Subtotal de la Fila Editada
            decimal nuevoSubtotal = cantidad * precio; 
            row.Cells["Subtotal"].Value = nuevoSubtotal;

            // Recalcular Total General
            CalcularTotal();
        }

        
        private void CalcularTotal()
        {
            decimal total = 0m; 
            CultureInfo culture = CultureInfo.GetCultureInfo("es-AR");

            foreach (DataGridViewRow row in dgvDetallesRecepcion.Rows)
            {
                // Sumar solo si el Subtotal es válido
                if (row.Cells["Subtotal"].Value != null)
                {
                    if (decimal.TryParse(row.Cells["Subtotal"].Value.ToString(), out decimal subtotal))
                    {
                        total += subtotal;
                    }
                    else if (decimal.TryParse(row.Cells["Subtotal"].Value.ToString(), NumberStyles.Currency, culture, out subtotal))
                    {
                        total += subtotal;
                    }
                }
            }
            txtTotalFinal.Text = total.ToString("C2", culture);
        }

        // Manejador del evento click del botón Confirmar Ingreso
        private void btnConfirmarIngreso_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show("¿Confirma la recepción de estos artículos? El stock será actualizado permanentemente.", "Confirmar Ingreso", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.No) return;

            CultureInfo culture = CultureInfo.GetCultureInfo("es-AR");

            // Validar que el total final sea un decimal válido
            if (!decimal.TryParse(txtTotalFinal.Text, NumberStyles.Currency, culture, out decimal totalFinal))
            {
                MessageBox.Show("El Total Final no es válido. Recalcule la orden.", "Error de Total", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var compraFinal = new Compra
            {
                Fecha = DateTime.Now,
                IdProveedor = _ordenActual.IdProveedor,
                Total = totalFinal,
                Tipo = _ordenActual.Tipo,
                IdOrdenCompra = _ordenActual.IdOrdenCompra,
                Detalles = new System.Collections.Generic.List<DetalleCompra>(),
                MetodoPago = cmbMetodoPago.SelectedItem.ToString()
            };

            // Lógica para llenar DetalleCompra con los datos FINALES de la grilla
            foreach (DataGridViewRow row in dgvDetallesRecepcion.Rows)
            {
                int cantidad = Convert.ToInt32(row.Cells["CantidadRecibida"].Value);
                decimal precio;

                // Obtenemos el TEXTO de la celda 
                string precioString = row.Cells["PrecioFinal"].Value?.ToString() ?? "0";

               
                if (!decimal.TryParse(precioString, NumberStyles.Currency, culture, out precio))
                {
                    decimal.TryParse(precioString, out precio);
                }

                if (cantidad > 0)
                {
                    compraFinal.Detalles.Add(new DetalleCompra
                    {
                        IdArticulo = Convert.ToInt32(row.Cells["IdArticulo"].Value),
                        Cantidad = cantidad,
                        PrecioUnitario = precio 
                    });
                }
            }

            // Validar que no esté vacío si el total es positivo
            if (compraFinal.Detalles.Count == 0 && totalFinal > 0)
            {
                MessageBox.Show("La orden está vacía, pero el total es positivo. Por favor, revise la cantidad y precio en la grilla.", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _comprasService.RegistrarIngresoDesdeOrden(compraFinal);
                MessageBox.Show("¡Ingreso registrado y stock actualizado con éxito!.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al registrar el ingreso: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}