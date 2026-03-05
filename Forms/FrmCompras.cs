using System.Data;
using System.Diagnostics;
using CasaRepuestos.Models;
using CasaRepuestos.Services;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.ComponentModel;

namespace CasaRepuestos.Forms
{

    public partial class FrmCompras : Form
    {
    
        private readonly ComprasService _comprasService = new ComprasService();
        private readonly ProveedorService _proveedorService = new ProveedorService();
        private readonly ArticuloService _articuloService = new ArticuloService();
        private readonly ArqueoService _arqueoService = new ArqueoService();

     
        private List<ArticuloFaltante> _repuestosFaltantesConsolidados = new List<ArticuloFaltante>();

 
        private BindingList<DetalleOrdenCompra> _detallesOrdenActual = new BindingList<DetalleOrdenCompra>();


        public FrmCompras()
        {
            InitializeComponent();
            CargarProveedoresYArticulos(); 
            ConfigurarGrillaNecesidades();
            ConfigurarGrillaOrdenStock();
            ConfigurarGrillaOrdenesPendientes();
            ConfigurarGrillaHistorial();
            AplicarEstilos();

            btnBuscarFaltantes.Click += btnBuscarFaltantes_Click;

            cmbFiltroTipoHistorial.Items.Add("Todos");
            cmbFiltroTipoHistorial.Items.Add("VENTA");
            cmbFiltroTipoHistorial.Items.Add("REPUESTO");
            cmbFiltroTipoHistorial.SelectedIndex = 0;
            btnBuscarHistorial.Click += btnBuscarHistorial_Click;
            CargarOrdenesPendientes();
        }

       

        private void AplicarEstilos()
        {

            this.BackColor = Color.FromArgb(240, 245, 249);

       
            foreach (var tp in EncontrarControlesRecursivos<TabPage>(this))
            {
                tp.BackColor = Color.White;
            }

       
            foreach (var gb in EncontrarControlesRecursivos<GroupBox>(this))
            {
                gb.BackColor = Color.White;
                gb.ForeColor = Color.FromArgb(45, 66, 91);
            }

          
            foreach (var dgv in EncontrarControlesRecursivos<DataGridView>(this))
            {
                if (dgv != null)
                {
                    dgv.BackgroundColor = Color.White;
                    dgv.EnableHeadersVisualStyles = false;

          
                    dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 66, 91);
                    dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
          
                    dgv.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
                    dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(4);
                    dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;


                    dgv.RowsDefaultCellStyle.BackColor = Color.White;

                    dgv.RowsDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9F);
                    dgv.RowsDefaultCellStyle.ForeColor = Color.FromArgb(40, 40, 40);
                    dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(240, 245, 249); 

         
                    dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
                    dgv.GridColor = Color.FromArgb(221, 233, 245); 
                    dgv.BorderStyle = BorderStyle.None;
                }
            }


            foreach (var btn in EncontrarControlesRecursivos<Button>(this))
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
                btn.BackColor = Color.FromArgb(76, 132, 200); 
                btn.ForeColor = Color.White;

                btn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
                btn.Padding = new Padding(4); 
            }


            try
            {
                btnCrearOrdenStock.BackColor = Color.FromArgb(34, 139, 34); 
                btnCrearOrdenStock.ForeColor = Color.White;
 
                btnCrearOrdenStock.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold); 
            }
            catch { }

            try
            {
                btnBuscarFaltantes.BackColor = Color.FromArgb(12, 87, 150); 
                btnBuscarHistorial.BackColor = Color.FromArgb(12, 87, 150); 
            }
            catch { }

        }
        private IEnumerable<T> EncontrarControlesRecursivos<T>(Control controlPadre) where T : Control
        {
            var controlesEncontrados = new List<T>();

            foreach (Control ctl in controlPadre.Controls)
            {
              
                if (ctl is T)
                {
                    controlesEncontrados.Add((T)ctl);
                }

     
                if (ctl.Controls.Count > 0)
                {
                    controlesEncontrados.AddRange(EncontrarControlesRecursivos<T>(ctl));
                }
            }
            return controlesEncontrados;
        }
        private void CargarProveedoresYArticulos()
        {
            try
            {
                // Se obtiene la lista completa de artículos desde el servicio
                var listaDeArticulos = _articuloService.ListarArticulos();
                // Configura el ComboBox para mostrar artículos
                cmbArticuloStock.DataSource = listaDeArticulos.OrderBy(a => a.Nombre).ToList();
                cmbArticuloStock.DisplayMember = "Nombre";       
                cmbArticuloStock.ValueMember = "IdArticulo";

                // Habilita el autocompletado
                cmbArticuloStock.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                cmbArticuloStock.AutoCompleteSource = AutoCompleteSource.ListItems;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar artículos: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void ConfigurarGrillaOrdenStock()
        {
            dgvOrdenStock.AutoGenerateColumns = false;


            if (dgvOrdenStock.Columns["NombreArticuloCol"] is DataGridViewTextBoxColumn nombreCol)
            {
                nombreCol.DataPropertyName = "ArticuloNombre";
            }


            if (dgvOrdenStock.Columns.Contains("ProveedorCol"))
            {
                dgvOrdenStock.Columns.Remove("ProveedorCol");
            }

            if (dgvOrdenStock.Columns["CantidadCol"] is DataGridViewTextBoxColumn cantidadCol)
            {
                cantidadCol.DataPropertyName = "CantidadSolicitada";
                cantidadCol.ReadOnly = false;
            }

            if (dgvOrdenStock.Columns["PrecioUnitarioCol"] is DataGridViewTextBoxColumn precioCol)
            {
                precioCol.DataPropertyName = "PrecioUnitarioEstimado";
                precioCol.ReadOnly = false;
            }

            List<Proveedor> listaMaestraProveedores;
            try
            {
                listaMaestraProveedores = _proveedorService.ListarProveedores() ?? new List<Proveedor>();


                if (!listaMaestraProveedores.Any(p => p.IdProveedor == 0))
                {
                    listaMaestraProveedores.Insert(0, new Proveedor { IdProveedor = 0, RazonSocial = "[Seleccionar Proveedor]" });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar la lista maestra de proveedores: {ex.Message}", "Error Crítico", MessageBoxButtons.OK, MessageBoxIcon.Error);
                listaMaestraProveedores = new List<Proveedor> { new Proveedor { IdProveedor = 0, RazonSocial = "[Error al Cargar]" } };
            }

            var cmbColProveedor = new DataGridViewComboBoxColumn
            {
                Name = "ProveedorCol",
                HeaderText = "Proveedor",
                DataPropertyName = "IdProveedor",
                DisplayMember = "RazonSocial",
                ValueMember = "IdProveedor",

                DataSource = listaMaestraProveedores,

                Width = 150,
                FlatStyle = FlatStyle.Flat
            };



            dgvOrdenStock.Columns.Insert(3, cmbColProveedor);

            dgvOrdenStock.DataSource = _detallesOrdenActual;

            dgvOrdenStock.CellEndEdit -= dgvOrdenStock_CellEndEdit;
            dgvOrdenStock.CellEndEdit += dgvOrdenStock_CellEndEdit;
            dgvOrdenStock.EditingControlShowing -= dgvOrdenStock_EditingControlShowing;
            dgvOrdenStock.EditingControlShowing += dgvOrdenStock_EditingControlShowing;

            dgvOrdenStock.CellValueChanged -= dgvOrdenStock_CellValueChanged;
            dgvOrdenStock.CellValueChanged += dgvOrdenStock_CellValueChanged;
            dgvOrdenStock.DataError -= dgvOrdenStock_DataError;
            dgvOrdenStock.DataError += dgvOrdenStock_DataError;
        }
        private void ConfigurarGrillaNecesidades()
        {
            dgvNecesidades.AutoGenerateColumns = false;
            dgvNecesidades.Columns.Clear();

            dgvNecesidades.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "IdArticulo", HeaderText = "ID", Name = "IdArticulo", Width = 50 });
            dgvNecesidades.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "NombreArticulo", HeaderText = "Artículo Faltante", Name = "NombreArticulo", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvNecesidades.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CantidadFaltante", HeaderText = "Cant. Faltante", Name = "CantidadFaltante", Width = 100 });

            

            var btnCol = new DataGridViewButtonColumn
            {
                Text = "Añadir a Orden",
                UseColumnTextForButtonValue = true,
                Name = "AnadirAOrden",
                HeaderText = "Acción",
                Width = 120
            };
            dgvNecesidades.Columns.Add(btnCol);
        }

        private void CargarNecesidadesRepuestos()
        {
            try
            {
                _repuestosFaltantesConsolidados = _comprasService.ObtenerNecesidadesRepuestosConsolidadas();

                // Asigna la lista 
                dgvNecesidades.DataSource = _repuestosFaltantesConsolidados;

                // Actualizar el mensaje
                label5.Text = _repuestosFaltantesConsolidados.Any()
                    ? $"Repuestos Faltantes Consolidados de presupuestos ESPERA_REPUESTOS ({_repuestosFaltantesConsolidados.Count} ítems)."
                    : "No hay repuestos en estado 'ESPERA_REPUESTOS' que necesiten ser comprados.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar las necesidades de repuestos: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Carga la lista de órdenes pendientes 
        private void CargarOrdenesPendientes()
        {
            try
            {
                //  Listar órdenes pendientes desde el servicio
                dgvOrdenesPendientes.DataSource = _comprasService.ListarOrdenesPendientes();

                
                if (!dgvOrdenesPendientes.Columns.Contains("RegistrarIngreso"))
                {
                    
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar órdenes pendientes: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ConfigurarGrillaOrdenesPendientes()
        {
            dgvOrdenesPendientes.AutoGenerateColumns = false;

            // Columnas de Órdenes Pendientes
            dgvOrdenesPendientes.Columns.Clear();
            dgvOrdenesPendientes.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "IdOrdenCompra", HeaderText = "ID", Name = "IdOrdenCompra", Width = 60 });
            dgvOrdenesPendientes.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FechaCreacion", HeaderText = "Fecha Creación", Name = "FechaCreacion", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            dgvOrdenesPendientes.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProveedorNombre", HeaderText = "Proveedor", Name = "ProveedorNombre", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvOrdenesPendientes.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TotalEstimado", HeaderText = "Total Estimado", Name = "TotalEstimado", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" } });
            dgvOrdenesPendientes.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Tipo", HeaderText = "Tipo", Name = "Tipo", Width = 80 });

            // Botones de acción
            dgvOrdenesPendientes.Columns.Add(new DataGridViewButtonColumn { Text = "PDF", UseColumnTextForButtonValue = true, Name = "ExportarPDF", HeaderText = "PDF", Width = 60 });
            dgvOrdenesPendientes.Columns.Add(new DataGridViewButtonColumn { Text = "Recibir", UseColumnTextForButtonValue = true, Name = "RegistrarIngreso", HeaderText = "Ingreso", Width = 70 });


            // Eventos para los botones
            dgvOrdenesPendientes.CellContentClick -= dgvOrdenesPendientes_CellContentClick;
            dgvOrdenesPendientes.CellContentClick += dgvOrdenesPendientes_CellContentClick;
        }


        private void ConfigurarGrillaHistorial()
        {
            dgvHistorial.AutoGenerateColumns = false;
            dgvHistorial.Columns.Clear();

            // Columnas de Historial
            dgvHistorial.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "IdCompra", HeaderText = "ID", Name = "IdCompra", Width = 60 });
            dgvHistorial.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Fecha", HeaderText = "Fecha", Name = "FechaCompra", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            dgvHistorial.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Tipo", HeaderText = "Tipo", Name = "TipoCompra", Width = 80 });
            dgvHistorial.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Proveedor", HeaderText = "Proveedor", Name = "ProveedorHistorial", Width = 150 });
            dgvHistorial.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ArticulosResumen", HeaderText = "Artículos (Resumen)", Name = "ArticulosResumen", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvHistorial.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Total", HeaderText = "Total", Name = "TotalCompra", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" } });
        }


        // -----------------------------------------------------
        // MÉTODOS PARA ARMAR LA ORDEN DE COMPRA 
        // -----------------------------------------------------

        private void btnAgregarItemStock_Click(object sender, EventArgs e)
        {
            // Verificamos SelectedItem 
            if (cmbArticuloStock.SelectedItem == null || (int)numCantidadStock.Value <= 0)
            {
                MessageBox.Show("Seleccione un artículo y una cantidad válida.", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Obtenemos el objeto Articulo 
            var itemSeleccionado = (Articulo)cmbArticuloStock.SelectedItem;
            if (itemSeleccionado == null) return;

            // Llamamos a AgregarItemAOrden 
            AgregarItemAOrden(
                itemSeleccionado.IdArticulo,
                itemSeleccionado.Nombre,       
                (int)numCantidadStock.Value,
                itemSeleccionado.PrecioCosto,
                0,                         
                "",                            
                "VENTA"
            );

            numCantidadStock.Value = 1;
        }
        // Lógica para agregar repuestos faltantes a la orden desde la grilla de necesidades
        private void dgvNecesidades_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || dgvNecesidades.Columns[e.ColumnIndex].Name != "AnadirAOrden") return;

            var repuestoFaltante = dgvNecesidades.Rows[e.RowIndex].DataBoundItem as ArticuloFaltante;
            if (repuestoFaltante == null) return;

            //  Guardamos el ID del artículo
            int idArticuloAgregado = repuestoFaltante.IdArticulo;

            // Agregamos a la orden 
            AgregarItemAOrden(
                repuestoFaltante.IdArticulo,
                repuestoFaltante.NombreArticulo,
                repuestoFaltante.CantidadFaltante,
                repuestoFaltante.PrecioUnitarioSugerido,
                0, 
                "", 
                "REPUESTO"
            );

            //  Verificamos si se agregó correctamente
            bool agregadoExitosamente = _detallesOrdenActual.Any(item => item.IdArticulo == idArticuloAgregado);

            if (agregadoExitosamente)
            {
                // Eliminamos el repuesto de la lista de necesidades 
                _repuestosFaltantesConsolidados.Remove(repuestoFaltante);

                // Refrescamos la grilla de la derecha 
                dgvNecesidades.DataSource = null;
                dgvNecesidades.DataSource = _repuestosFaltantesConsolidados;

                // Actualizamos el contador
                label5.Text = _repuestosFaltantesConsolidados.Any()
                    ? $"Repuestos Faltantes... ({_repuestosFaltantesConsolidados.Count} ítems)."
                    : "No hay repuestos que necesiten ser comprados.";
            }
        }

        private void AgregarItemAOrden(int idArticulo, string nombreArticulo, int cantidad, decimal precioUnitario, int idProveedor, string proveedorNombre, string tipoIntencion) // 💡 CAMBIO: decimal
        {
            if (cantidad <= 0) return;

            var itemExistente = _detallesOrdenActual.FirstOrDefault(d => d.IdArticulo == idArticulo);

            if (itemExistente != null)
            {
                MessageBox.Show($"El artículo '{nombreArticulo}' ya está en la orden.", "Artículo Duplicado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _detallesOrdenActual.Add(new DetalleOrdenCompra
            {
                IdArticulo = idArticulo,
                ArticuloNombre = nombreArticulo,
                CantidadSolicitada = cantidad,
                PrecioUnitarioEstimado = precioUnitario, 
                IdProveedor = 0,
                ProveedorNombre = "[SELECCIONAR]",
                TipoIntencion = tipoIntencion
            });

            CalcularTotalOrden();
        }

        private void CalcularTotalOrden()
        {
            decimal total = 0m; 
            foreach (var detalle in _detallesOrdenActual)
            {
                total += detalle.CantidadSolicitada * detalle.PrecioUnitarioEstimado;
            }

            txtTotalOrden.Text = total.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("es-AR"));
        }
        // Manejo de eventos en la grilla de la orden de stock
        private void dgvOrdenStock_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            // Eliminar ítem de la orden
            if (dgvOrdenStock.Columns[e.ColumnIndex].Name == "EliminarCol")
            {
                if (MessageBox.Show("¿Está seguro de eliminar este ítem de la orden?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    var item = dgvOrdenStock.Rows[e.RowIndex].DataBoundItem as DetalleOrdenCompra;
                    if (item != null)
                    {
                        _detallesOrdenActual.Remove(item);

                        
                        CalcularTotalOrden();

                    }
                }
            }
        }

        // -----------------------------------------------------
        // MÉTODOS PARA CREAR LA ORDEN
        // -----------------------------------------------------
        private void btnCrearOrdenStock_Click(object sender, EventArgs e)
        {
            // Verificar si la caja está cerrada
            if (_arqueoService.EsCajaDelDiaCerrada())
            {
                MessageBox.Show(
                    "No se pueden realizar compras.\n\nLa caja del día ya ha sido cerrada.\nPara realizar transacciones, debe reabrir la caja desde el módulo de Arqueo.",
                    "Caja Cerrada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Verificar si no hay caja abierta
            if (!_arqueoService.ExisteCajaAbiertaHoy())
            {
                MessageBox.Show(
                    "No se pueden realizar compras.\n\nNo hay una caja abierta para el día de hoy.\nPor favor, abra la caja desde el módulo de Arqueo antes de realizar transacciones.",
                    "Caja No Abierta",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (_detallesOrdenActual.Count == 0)
            {
                MessageBox.Show("Debe añadir al menos un artículo a la orden.", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

           
            var itemSinProveedor = _detallesOrdenActual.FirstOrDefault(d => d.IdProveedor == 0);
            if (itemSinProveedor != null)
            {
                MessageBox.Show($"Debe seleccionar un proveedor para el artículo '{itemSinProveedor.ArticuloNombre}'.", "Proveedor Faltante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show("¿Confirmar la creación de la(s) Orden(es) de Compra?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
            {
                return;
            }
            try
            {
                // Muestra un resumen antes de crear las órdenes
                var gruposPorProveedorYTipo = _detallesOrdenActual
                    .Where(d => d.IdProveedor > 0) 
                    .GroupBy(detalle => new { detalle.IdProveedor, detalle.TipoIntencion }) 
                    .ToList();

                if (!gruposPorProveedorYTipo.Any())
                {
                    MessageBox.Show("No se pueden crear órdenes: Algún artículo agregado no tiene proveedor asignado.", "Error de Proveedor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var pdfsGenerados = new List<string>();
                var fechaDeEmision = DateTime.Now;
                var idsOrdenesPorProveedor = new Dictionary<int, List<int>>();
                // iterar sobre cada grupo para crear órdenes separadas
                foreach (var grupo in gruposPorProveedorYTipo)
                {
                    // Declarar y asignar el ID del proveedor del grupo
                    int idProveedorActual = grupo.Key.IdProveedor;


                    // Crear una Orden de Compra POR CADA grupo
                    var nuevaOrden = new OrdenCompra
                    {
                        FechaCreacion = fechaDeEmision,
                        IdProveedor = idProveedorActual, 
                        TotalEstimado = grupo.Sum(d => d.Subtotal),
                        Tipo = grupo.Key.TipoIntencion,
                        Estado = "PENDIENTE",
                        Detalles = grupo.ToList()
                    };

                    int idOrdenGenerada = _comprasService.CrearOrdenDeCompra(nuevaOrden);

                    // idProveedorActual
                    if (!idsOrdenesPorProveedor.ContainsKey(idProveedorActual))
                    {
                        idsOrdenesPorProveedor[idProveedorActual] = new List<int>();
                    }
                    idsOrdenesPorProveedor[idProveedorActual].Add(idOrdenGenerada);
                }

                
                int documentosGenerados = 0;
                foreach (var kvp in idsOrdenesPorProveedor)
                {
                    var nombreProveedor = _detallesOrdenActual.First(d => d.IdProveedor == kvp.Key).ProveedorNombre;

                    // Llama al nuevo método para generar UN PDF por proveedor
                    ExportarOrdenesConsolidadasPDF(kvp.Value, nombreProveedor);
                    documentosGenerados++;
                }

                // Mostrar mensaje de éxito y limpiar
                MessageBox.Show(
                    $"¡Éxito! Se generaron {gruposPorProveedorYTipo.Count} órdenes internas, consolidadas en {documentosGenerados} documentos PDF.\n" +
                    $"Puede verlos en la pestaña 'Órdenes Pendientes'.",
                    "Órdenes Creadas",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                _detallesOrdenActual.Clear();
                CalcularTotalOrden();
                CargarNecesidadesRepuestos();
                tabControl.SelectedTab = tabOrdenesPendientes;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al crear las Órdenes de Compra: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // -----------------------------------------------------
        // MÉTODOS DE PENDIENTES E HISTORIAL
        // -----------------------------------------------------
        private string ExportarOrdenesConsolidadasPDF(List<int> idsOrdenes, string proveedorNombre)
        {
            if (!idsOrdenes.Any()) return null;

            try
            {
                // Obtener todos los detalles y el total
                var detallesConsolidados = new List<DetalleOrdenCompra>();
                decimal totalConsolidado = 0;
                DateTime fechaMasReciente = DateTime.MinValue;

                // Variable para almacenar el nombre del proveedor
                string nombreProveedorReal = proveedorNombre;

                foreach (int id in idsOrdenes)
                {
                    var orden = _comprasService.GetOrdenConDetalles(id);
                    if (orden == null) continue;

                    if (nombreProveedorReal == "[SELECCIONAR]" && !string.IsNullOrEmpty(orden.ProveedorNombre))
                    {
                        nombreProveedorReal = orden.ProveedorNombre;
                    }

                    detallesConsolidados.AddRange(orden.Detalles);
                    totalConsolidado += orden.TotalEstimado;
                    if (orden.FechaCreacion > fechaMasReciente)
                    {
                        fechaMasReciente = orden.FechaCreacion;
                    }
                }

                if (!detallesConsolidados.Any()) return null;

                // Configuración del archivo y ruta
                string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string nombreProveedorLimpio = string.Join("_", nombreProveedorReal.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(folderPath, $"OC_CONSOLIDA_{nombreProveedorLimpio}_{fechaMasReciente:yyyyMMddHHmmss}.pdf");

                // Generación del PDF 
                using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    Document doc = new Document(PageSize.A4);
                    PdfWriter.GetInstance(doc, fs);
                    doc.Open();

                    // Fuentes
                    BaseFont bf = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                    iTextSharp.text.Font fontTitulo = new iTextSharp.text.Font(bf, 16, iTextSharp.text.Font.BOLD);
                    iTextSharp.text.Font fontHeader = new iTextSharp.text.Font(bf, 11, iTextSharp.text.Font.BOLD, BaseColor.WHITE);
                    iTextSharp.text.Font fontTexto = new iTextSharp.text.Font(bf, 10, iTextSharp.text.Font.NORMAL);
                    iTextSharp.text.Font fontTextoBold = new iTextSharp.text.Font(bf, 10, iTextSharp.text.Font.BOLD);

                    // Título
                    Paragraph titulo = new Paragraph($"Orden(es) de Compra Consolidada - Proveedor: {nombreProveedorReal}", fontTitulo);
                    titulo.Alignment = Element.ALIGN_CENTER;
                    doc.Add(titulo);
                    doc.Add(Chunk.NEWLINE);

                    // Información de Órdenes consolidadas
                    doc.Add(new Paragraph($"Fechas de Emisión: {fechaMasReciente:dd/MM/yyyy}", fontTexto));
                    doc.Add(new Paragraph($"Incluye Órdenes: {string.Join(", ", idsOrdenes)}", fontTexto));
                    doc.Add(Chunk.NEWLINE);

                    PdfPTable table = new PdfPTable(4);
                    table.WidthPercentage = 100;
                    
                    float[] anchos = { 0.5f, 0.15f, 0.15f, 0.2f };
                    table.SetWidths(anchos);

                    // Encabezados de Tabla
                    BaseColor headerColor = new BaseColor(30, 30, 30);
                    AñadirCelda(table, "Artículo", fontHeader, Element.ALIGN_CENTER, headerColor);
                    AñadirCelda(table, "Cantidad", fontHeader, Element.ALIGN_CENTER, headerColor);
                    AñadirCelda(table, "P. Unitario", fontHeader, Element.ALIGN_RIGHT, headerColor);
                    AñadirCelda(table, "Subtotal", fontHeader, Element.ALIGN_RIGHT, headerColor);
              
                    var detallesAgrupados = detallesConsolidados
                        .GroupBy(det => new { det.IdArticulo, det.ArticuloNombre, det.PrecioUnitarioEstimado })
                        .Select(g => new
                        {
                            g.Key.ArticuloNombre,
                            CantidadTotal = g.Sum(d => d.CantidadSolicitada),
                            g.Key.PrecioUnitarioEstimado,
                            SubtotalTotal = g.Sum(d => d.Subtotal) 
                        });

                    // Filas de Detalles 
                    foreach (var det in detallesAgrupados)
                    {
                        AñadirCelda(table, det.ArticuloNombre, fontTexto, Element.ALIGN_LEFT);
                        AñadirCelda(table, det.CantidadTotal.ToString(), fontTexto, Element.ALIGN_CENTER);
                        AñadirCelda(table, det.PrecioUnitarioEstimado.ToString("C2"), fontTexto, Element.ALIGN_RIGHT);
                        AñadirCelda(table, det.SubtotalTotal.ToString("C2"), fontTexto, Element.ALIGN_RIGHT);
                        ; 
                    }

                    doc.Add(table);
                    doc.Add(Chunk.NEWLINE);

                    // Total
                    Paragraph total = new Paragraph($"TOTAL CONSOLIDADO: {totalConsolidado:C2}", fontTextoBold);
                    total.Alignment = Element.ALIGN_RIGHT;
                    doc.Add(total);

                    doc.Close();
                }

                MessageBox.Show($"Órdenes consolidadas exportadas con éxito a:\n{filePath}", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });

                return filePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar el PDF consolidado: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }
        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab == tabOrdenesPendientes)
            {
                CargarOrdenesPendientes();
                CargarNecesidadesRepuestos();
            }

            else if (tabControl.SelectedTab == tabHistorial)
            {
                // Cargar historial con filtros por defecto
                btnBuscarHistorial_Click(null, null);
            }
        }

        private void dgvOrdenesPendientes_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            int idOrden = Convert.ToInt32(dgvOrdenesPendientes.Rows[e.RowIndex].Cells["IdOrdenCompra"].Value);
            if (dgvOrdenesPendientes.Columns[e.ColumnIndex].Name == "ExportarPDF")
            {
                ExportarOrdenPDF(idOrden, abrirPdf: true);
            }

            if (dgvOrdenesPendientes.Columns[e.ColumnIndex].Name == "RegistrarIngreso")
            {
                var frmRecepcion = new FrmRecepcionCompra(idOrden);
                frmRecepcion.ShowDialog();
                CargarOrdenesPendientes();
                CargarNecesidadesRepuestos();
            }
        }
        private string ExportarOrdenPDF(int idOrden, bool abrirPdf = true)
        {
            try
            {
                var orden = _comprasService.GetOrdenConDetalles(idOrden);
                if (orden == null) throw new Exception($"No se encontró la Orden N°{idOrden}.");

                // Ruta a la carpeta de Descargas del usuario
                string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string nombreProveedorLimpio = string.Join("_", orden.ProveedorNombre.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(folderPath, $"OC_{idOrden}_{nombreProveedorLimpio}.pdf");

                using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    Document doc = new Document(PageSize.A4);
                    PdfWriter.GetInstance(doc, fs);
                    doc.Open();
                    
                    BaseFont bf = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);

                    iTextSharp.text.Font fontTitulo = new iTextSharp.text.Font(bf, 16, iTextSharp.text.Font.BOLD);
                    iTextSharp.text.Font fontHeader = new iTextSharp.text.Font(bf, 11, iTextSharp.text.Font.BOLD, BaseColor.WHITE);
                    iTextSharp.text.Font fontTexto = new iTextSharp.text.Font(bf, 10, iTextSharp.text.Font.NORMAL);
                    iTextSharp.text.Font fontTextoBold = new iTextSharp.text.Font(bf, 10, iTextSharp.text.Font.BOLD);
                    // Título
                    Paragraph titulo = new Paragraph($"Orden de Compra N° {orden.IdOrdenCompra}", fontTitulo);
                    titulo.Alignment = Element.ALIGN_CENTER;
                    doc.Add(titulo);
                    doc.Add(Chunk.NEWLINE);

                    //  Datos del Proveedor y Fecha
                    doc.Add(new Paragraph($"Proveedor: {orden.ProveedorNombre}", fontTexto));
                    doc.Add(new Paragraph($"Fecha de Emisión: {orden.FechaCreacion:dd/MM/yyyy}", fontTexto));
                    doc.Add(Chunk.NEWLINE);

                    // Crear Tabla de Detalles
                    PdfPTable table = new PdfPTable(5); 
                    table.WidthPercentage = 100;
                    float[] anchos = { 0.1f, 0.4f, 0.15f, 0.15f, 0.2f };
                    table.SetWidths(anchos);

                    BaseColor headerColor = new BaseColor(30, 30, 30); 
                    AñadirCelda(table, "ID Art.", fontHeader, Element.ALIGN_CENTER, headerColor);
                    AñadirCelda(table, "Artículo", fontHeader, Element.ALIGN_CENTER, headerColor);
                    AñadirCelda(table, "Cantidad", fontHeader, Element.ALIGN_CENTER, headerColor);
                    AñadirCelda(table, "P. Unitario", fontHeader, Element.ALIGN_RIGHT, headerColor);
                    AñadirCelda(table, "Subtotal", fontHeader, Element.ALIGN_RIGHT, headerColor);

                    // Filas de Detalles
                    foreach (var det in orden.Detalles)
                    {
                        AñadirCelda(table, det.IdArticulo.ToString(), fontTexto, Element.ALIGN_CENTER);
                        AñadirCelda(table, det.ArticuloNombre, fontTexto, Element.ALIGN_LEFT);
                        AñadirCelda(table, det.CantidadSolicitada.ToString(), fontTexto, Element.ALIGN_CENTER);
                        AñadirCelda(table, det.PrecioUnitarioEstimado.ToString("C2"), fontTexto, Element.ALIGN_RIGHT);
                        // Calculamos el subtotal del detalle
                        decimal subtotalDetalle = det.CantidadSolicitada * det.PrecioUnitarioEstimado;
                        AñadirCelda(table, subtotalDetalle.ToString("C2"), fontTexto, Element.ALIGN_RIGHT);
                    }

                    // Añadir la tabla al documento
                    doc.Add(table);
                    doc.Add(Chunk.NEWLINE);

                    // Total
                    Paragraph total = new Paragraph($"Total Estimado: {orden.TotalEstimado:C2}", fontTextoBold);
                    total.Alignment = Element.ALIGN_RIGHT;
                    doc.Add(total);


                    doc.Close(); 
                }

                if (abrirPdf)
                {
                    MessageBox.Show($"Orden de Compra N°{idOrden} exportada con éxito a:\n{filePath}", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }

                return filePath; 
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar el PDF: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null; 
            }
        }
        private void AñadirCelda(iTextSharp.text.pdf.PdfPTable table, string text, iTextSharp.text.Font font, int align, iTextSharp.text.BaseColor? bgColor = null)
        {
            iTextSharp.text.pdf.PdfPCell cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(text, font))
            {
                HorizontalAlignment = align,
                VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE,
                Padding = 5f,
                BackgroundColor = bgColor ?? iTextSharp.text.BaseColor.WHITE
            };
            table.AddCell(cell);
        }
        private void btnBuscarHistorial_Click(object sender, EventArgs e)
        {
            try
            {
                //Obtener Filtros
                string tipoFiltro = cmbFiltroTipoHistorial.SelectedItem.ToString();

                
                string tipo = (tipoFiltro == "Todos") ? "" : tipoFiltro.ToUpper();

            
                DateTime? desde = dtpDesdeHistorial.Value.Date;
                DateTime? hasta = dtpHastaHistorial.Value.Date;

                // Llamar al servicio y asignar resultados
                dgvHistorial.DataSource = _comprasService.ListarHistorialCompras(tipo, desde, hasta);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al buscar historial: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnBuscarFaltantes_Click(object sender, EventArgs e)
        {
            CargarNecesidadesRepuestos();
        }

        private void dgvOrdenStock_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var item = dgvOrdenStock.Rows[e.RowIndex].DataBoundItem as DetalleOrdenCompra;
            if (item == null) return;

            var currentCulture = System.Globalization.CultureInfo.CurrentCulture;

            try
            {
                if (dgvOrdenStock.Columns[e.ColumnIndex].Name == "CantidadCol")
                {
                    if (int.TryParse(Convert.ToString(dgvOrdenStock.Rows[e.RowIndex].Cells["CantidadCol"].Value), out int nuevaCantidad) && nuevaCantidad > 0)
                    {
                        item.CantidadSolicitada = nuevaCantidad;
                    }
                    else
                    {
                        MessageBox.Show("Cantidad debe ser un número entero positivo.", "Error de Cantidad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        // Revertir el valor en la celda
                        dgvOrdenStock.Rows[e.RowIndex].Cells["CantidadCol"].Value = item.CantidadSolicitada;
                    }
                }
                else if (dgvOrdenStock.Columns[e.ColumnIndex].Name == "PrecioUnitarioCol")
                {
                    string valorCelda = Convert.ToString(dgvOrdenStock.Rows[e.RowIndex].Cells["PrecioUnitarioCol"].Value);

                    if (decimal.TryParse(valorCelda, System.Globalization.NumberStyles.Currency,
                                         currentCulture, out decimal nuevoPrecio) && nuevoPrecio >= 0)
                    {
                        
                        if (item.IdProveedor <= 0)
                        {
                            MessageBox.Show("Debe seleccionar un proveedor ANTES de modificar el precio.", "Acción Requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            // Revertimos el precio al estimado (el MAX)
                            item.PrecioUnitarioEstimado = _articuloService.ObtenerArticuloPorId(item.IdArticulo).PrecioCosto;
                            dgvOrdenStock.Rows[e.RowIndex].Cells["PrecioUnitarioCol"].Value = item.PrecioUnitarioEstimado.ToString("C2", currentCulture);
                        }
                        else
                        {
                           
                            item.PrecioUnitarioEstimado = nuevoPrecio;
                            _comprasService.ActualizarPrecioCostoProveedor(item.IdArticulo, item.IdProveedor, nuevoPrecio);
                        }
                    }
                    else
                    {
                        MessageBox.Show("El precio ingresado no es un formato de moneda válido.", "Error de Precio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        // Revertir
                        dgvOrdenStock.Rows[e.RowIndex].Cells["PrecioUnitarioCol"].Value = item.PrecioUnitarioEstimado.ToString("C2", currentCulture);
                    }
                }

                // Forzar el recálculo total y refresco de la fila
                CalcularTotalOrden();
                // Refrescar solo la fila actual para que el SubtotalCol muestre el nuevo valor
                dgvOrdenStock.InvalidateRow(e.RowIndex);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar la celda: {ex.Message}", "Error de Edición", MessageBoxButtons.OK, MessageBoxIcon.Error);
             

            }
        }

        private void dgvOrdenStock_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            // Verificamos si estamos en la columna ComboBox de Proveedor
            if (dgvOrdenStock.CurrentCell.ColumnIndex == dgvOrdenStock.Columns["ProveedorCol"].Index && e.Control is ComboBox cmbProveedorControl)
            {
                // Obtenemos el IdArticulo de la fila actual
                var detalle = dgvOrdenStock.Rows[dgvOrdenStock.CurrentRow.Index].DataBoundItem as DetalleOrdenCompra;
                if (detalle == null) return;

                try
                {
                    var articulo = _articuloService.ObtenerArticuloPorId(detalle.IdArticulo);
                    var todosProveedores = _proveedorService.ListarProveedores() ?? new List<Proveedor>();

                    var idsProveedoresAsignados = articulo.ProveedoresConCosto
                        .Select(pc => pc.IdProveedor)
                        .ToList();

                    
                    var proveedoresDelArticulo = todosProveedores
                        .Where(p => idsProveedoresAsignados.Contains(p.IdProveedor))
                        .ToList();

                    if (detalle.IdProveedor == 0)
                    {
                        proveedoresDelArticulo.Insert(0, new Proveedor { IdProveedor = 0, RazonSocial = "[Seleccionar Proveedor]" });
                    }
                    

                  
                    cmbProveedorControl.DataSource = null;
                    cmbProveedorControl.DataSource = proveedoresDelArticulo;
                    cmbProveedorControl.DisplayMember = "RazonSocial";
                    cmbProveedorControl.ValueMember = "IdProveedor";

                    // Aseguramos que el valor actual esté seleccionado
                    cmbProveedorControl.SelectedValue = detalle.IdProveedor;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al cargar proveedores para el artículo: " + ex.Message);
                }
            }
        }
        // Manejo del cambio de valor en la celda Proveedor
        private void dgvOrdenStock_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
       
            if (e.RowIndex < 0 || e.ColumnIndex != dgvOrdenStock.Columns["ProveedorCol"].Index) return;

            // Obtener el detalle (el objeto) de la fila que cambió
            var detalle = dgvOrdenStock.Rows[e.RowIndex].DataBoundItem as DetalleOrdenCompra;
            if (detalle == null) return;

            this.BeginInvoke(new Action(() =>
            {
                try
                {
                    var valorCelda = dgvOrdenStock.Rows[e.RowIndex].Cells["ProveedorCol"].Value;
                    if (valorCelda == null || valorCelda == DBNull.Value) return;

                    var idProveedorSeleccionado = Convert.ToInt32(valorCelda);

                    if (idProveedorSeleccionado > 0)
                    {
                        // Pedimos el precioCoste a la BD
                        decimal precioCoste = _articuloService.ObtenerPrecioCosto(detalle.IdArticulo, idProveedorSeleccionado);

                        
                        var proveedorSeleccionado = _proveedorService.ListarProveedores()
                                                    .FirstOrDefault(p => p.IdProveedor == idProveedorSeleccionado);

                        detalle.IdProveedor = idProveedorSeleccionado;
                        detalle.ProveedorNombre = proveedorSeleccionado?.RazonSocial ?? "N/A";

                       // ACTUALIZAMOS EL PRECIO EN MEMORIA Y EN LA CELDA
                        detalle.PrecioUnitarioEstimado = precioCoste;
                        dgvOrdenStock.Rows[e.RowIndex].Cells["PrecioUnitarioCol"].Value = precioCoste.ToString("F2");

                        // Recalculamos el total
                        CalcularTotalOrden();
                        dgvOrdenStock.InvalidateRow(e.RowIndex);
                    }
                    else
                    {
                        detalle.IdProveedor = 0;
                        detalle.ProveedorNombre = "[Seleccionar Proveedor]";
                        detalle.PrecioUnitarioEstimado = 0;
                        dgvOrdenStock.Rows[e.RowIndex].Cells["PrecioUnitarioCol"].Value = (0m).ToString("F2");
                        CalcularTotalOrden();
                        dgvOrdenStock.InvalidateRow(e.RowIndex);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al actualizar precio del proveedor: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }));
        }
        // Manejo de errores en la grilla de la orden de stock
        private void dgvOrdenStock_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (e.ColumnIndex == dgvOrdenStock.Columns["ProveedorCol"].Index)
            {
                e.ThrowException = false;

                e.Cancel = true;

            }
        }
    }
}