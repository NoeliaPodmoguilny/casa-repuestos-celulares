using CasaRepuestos.Models;
using CasaRepuestos.Services;
using System.Data;

namespace CasaRepuestos.Forms
{
    public partial class FrmReparaciones : Form
    {
        private readonly PresupuestoService _presupuestoService;
        private readonly ReparacionService _reparacionService;
        private readonly ServiciosService _servicioService = new ServiciosService();
        private List<Articulo> _articulosDisponibles = new();
        private List<Servicio> _serviciosDisponibles = new();

        //  detalles del presupuesto seleccionado (para editar)
        private List<DetallePresupuesto> _detallesActuales = new();
        private int _presupuestoSeleccionado = 0;

        public FrmReparaciones()
        {
            InitializeComponent();

            string connectionString = "Server=localhost;Database=casarepuestos;Uid=DPaz;Pwd=1234;";

            // Inicializa ambos servicios
            _presupuestoService = new PresupuestoService();
            _reparacionService = new ReparacionService();

            AplicarEstilos();
        }

        // Evento Load del formulario
        private void FrmReparaciones_Load(object sender, EventArgs e)
        {
            CargarCatalogos();
            CargarTodo();
            // Configurar DataGridView de detalles como solo lectura
            dgvDetalles.AllowUserToAddRows = false;
            dgvDetalles.AllowUserToDeleteRows = false;
        }

        private void AplicarEstilos()
        {
            this.BackColor = Color.FromArgb(240, 245, 249);

            // Aplicar estilos a GroupBoxes
            foreach (var gb in this.Controls.OfType<GroupBox>())
            {
                gb.BackColor = Color.White;
                gb.ForeColor = Color.FromArgb(45, 66, 91);
            }

            // Aplicar estilos a Botones
            foreach (var btn in this.Controls.OfType<Button>())
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
                btn.BackColor = Color.FromArgb(76, 132, 200);
                btn.ForeColor = Color.White;
            }

            // Estilo específico para btnMarcarReparado
            try { btnMarcarReparado.BackColor = Color.FromArgb(34, 139, 34); btnMarcarReparado.ForeColor = Color.White; } catch { }

            foreach (var dgv in new[] { dgvEsperando, dgvEnProceso, dgvDetalles })
            {
                if (dgv != null)
                {
                    dgv.BackgroundColor = Color.White;
                    dgv.EnableHeadersVisualStyles = false;
                    dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 66, 91);
                    dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                }
            }
        }
        // Cargar lista de artículos y servicios
        private void CargarCatalogos()
        {
            _articulosDisponibles = _presupuestoService.GetArticulos() ?? new List<Articulo>();
            _serviciosDisponibles = _presupuestoService.GetServicios() ?? new List<Servicio>();


        }

        private void CargarTodo()
        {
            try
            {

                // Ahora lee los estados que el método anterior ya corrigió
                DataTable dt = _reparacionService.ObtenerPresupuestosConEstadoStock();

                if (!dt.Columns.Contains("stock_norm"))
                    dt.Columns.Add("stock_norm", typeof(string));

                foreach (DataRow r in dt.Rows)
                {
                    string raw = r["stock_status"]?.ToString() ?? "";
                    r["stock_norm"] = NormalizeStockStatus(raw);
                }

                var dtEsperando = dt.Clone();
                var dtEnProceso = dt.Clone();
                var dtReparado = dt.Clone();

                // separar en tablas según estado
                foreach (DataRow r in dt.Rows)
                {
                    string stock = r["stock_norm"]?.ToString() ?? "";
                    switch (stock)
                    {
                        case "ESPERA_REPUESTOS":
                            dtEsperando.ImportRow(r);
                            break;
                        case "EN_PROCESO":
                        case "SIN_REPUESTOS":
                            dtEnProceso.ImportRow(r);
                            break;
                        case "FINALIZADO":
                            dtReparado.ImportRow(r);
                            break;
                        default:
                            dtEnProceso.ImportRow(r);
                            break;
                    }
                }

                dgvEsperando.DataSource = dtEsperando;
                dgvEnProceso.DataSource = dtEnProceso;


                // ajuste visual
                AjustarGridSimple(dgvEsperando);
                AjustarGridSimple(dgvEnProceso);


                // pintar filas por estado 
                PintarFilasSegunEstado(dgvEsperando, "ESPERA_REPUESTOS");
                PintarFilasSegunEstado(dgvEnProceso, "EN_PROCESO");


                // seleccionar primer elemento de la pestaña activa si existe
                SeleccionarPrimeraFilaSegunTab();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error cargando reparaciones: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // Ajusta columnas del DataGridView para mejor visualización
        private void AjustarGridSimple(DataGridView dgv)
        {
            if (dgv.Columns.Contains("idpresupuesto"))
            {
                dgv.Columns["idpresupuesto"].HeaderText = "ID";
                dgv.Columns["idpresupuesto"].Width = 60;
                dgv.Columns["idpresupuesto"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            if (dgv.Columns.Contains("marca"))
            {
                dgv.Columns["marca"].HeaderText = "Marca";
                dgv.Columns["marca"].Width = 150;
                dgv.Columns["marca"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            }
            if (dgv.Columns.Contains("modelo"))
            {
                dgv.Columns["modelo"].HeaderText = "Modelo";
                dgv.Columns["modelo"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            if (dgv.Columns.Contains("stock_status"))
            {
                dgv.Columns["stock_status"].HeaderText = "Stock";
                dgv.Columns["stock_status"].Width = 120;
            }

            // ocultar columna auxiliar 
            if (dgv.Columns.Contains("stock_norm"))
                dgv.Columns["stock_norm"].Visible = false;

            dgv.ClearSelection();
            dgv.CurrentCell = null;
        }

        private void SeleccionarPrimeraFilaSegunTab()
        {
            // quitar handlers antes de manipular selección
            dgvEsperando.SelectionChanged -= Grid_SelectionChanged;
            dgvEnProceso.SelectionChanged -= Grid_SelectionChanged;


            var dgv = dgvEsperando;
            if (tabControlPrincipal.SelectedTab == tabEnProceso) dgv = dgvEnProceso;


            if (dgv.Rows.Count > 0)
            {
                dgv.ClearSelection();
                dgv.Rows[0].Selected = true;
                CargarSeleccionadoDesdeGrid(dgv);
            }
            else
            {
                LimpiarDetalle();
            }

            // enganchar eventos nuevamente
            dgvEsperando.SelectionChanged += Grid_SelectionChanged;
            dgvEnProceso.SelectionChanged += Grid_SelectionChanged;

        }
        // Evento común para cambio de selección en grillas principales
        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            var dgv = sender as DataGridView;
            CargarSeleccionadoDesdeGrid(dgv);
        }

        // Obtener DataRowView del ítem seleccionado en el DataGridView
        private DataRowView GetSelectedRowView(DataGridView dgv)
        {
            if (dgv == null || dgv.SelectedRows.Count == 0) return null;
            return dgv.SelectedRows[0].DataBoundItem as DataRowView;
        }

        private void CargarSeleccionadoDesdeGrid(DataGridView dgv)
        {
            try
            {
                var drv = GetSelectedRowView(dgv);
                if (drv == null) { LimpiarDetalle(); return; }

                int id = Convert.ToInt32(drv["idpresupuesto"]);
                _presupuestoSeleccionado = id;

                // obtener ingreso (marca/modelo/falla) para mostrar detalles
                Ingreso ingreso = null;
                try
                {
                    ingreso = _presupuestoService.GetIngresoPorPresupuesto(id);
                }
                catch
                {
                    ingreso = null;
                }

                lblInfoModelo.Text = $"Marca: {ingreso?.Marca ?? "-"}  |  Modelo: {ingreso?.Modelo ?? "-"}";
                lblInfoFalla.Text = $"Falla: {ingreso?.Falla ?? "-"}";

                // cargar detalles (repuestos/servicios)
                var detalles = _presupuestoService.GetDetallesPresupuesto(id) ?? new List<DetallePresupuesto>();
                _detallesActuales = detalles;

                RecargarGridDetalles();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar seleccionado: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LimpiarDetalle()
        {
            lblInfoModelo.Text = "Marca: -  |  Modelo: -";
            lblInfoFalla.Text = "Falla: -";
            dgvDetalles.DataSource = null;
            dgvDetalles.Rows.Clear();
            _detallesActuales = new List<DetallePresupuesto>();
            _presupuestoSeleccionado = 0;
        }

        private void RecargarGridDetalles()
        {
            dgvDetalles.Columns.Clear();
            dgvDetalles.Rows.Clear();

            // Definición de columnas para SOLO VISUALIZACIÓN
            dgvDetalles.Columns.Add("Descripcion", "Descripción");
            dgvDetalles.Columns.Add("Cantidad", "Cantidad");
            dgvDetalles.Columns.Add("PrecioRepuesto", "Precio Repuesto");
            dgvDetalles.Columns.Add("PrecioServicio", "Precio Servicio");
            dgvDetalles.Columns.Add("Subtotal", "Subtotal");

            foreach (var d in _detallesActuales)
            {
                string descripcion = ObtenerDescripcionDetalle(d);
                decimal pr = d.PrecioRepuesto ?? 0m;
                decimal ps = d.PrecioServicio ?? 0m;
                decimal subtotal = (pr + ps) * d.Cantidad;

                dgvDetalles.Rows.Add(descripcion, d.Cantidad, pr.ToString("N2"), ps.ToString("N2"), subtotal.ToString("N2"));
            }

            // Establecer todas las celdas como NO EDITABLES
            foreach (DataGridViewColumn col in dgvDetalles.Columns)
            {
                col.ReadOnly = true;
            }

            // Ocultar cualquier columna de botón "Eliminar" si existe en el diseño
            if (dgvDetalles.Columns.Contains("Eliminar"))
                dgvDetalles.Columns["Eliminar"].Visible = false;

            dgvDetalles.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }


        private string ObtenerDescripcionDetalle(DetallePresupuesto d)
        {
            // Determina si es artículo o servicio y obtiene la descripción adecuada
            if (d.IdArticulo.HasValue && d.IdArticulo.Value > 0)
            {
                var art = _articulosDisponibles.FirstOrDefault(a => a.IdArticulo == d.IdArticulo);
                return art?.Nombre ?? $"Artículo ID: {d.IdArticulo.Value}";
            }
            else if (d.IdServicio > 0)
            {
                var serv = _serviciosDisponibles.FirstOrDefault(s => s.IdServicio == d.IdServicio);
                return serv?.Descripcion ?? $"Servicio ID: {d.IdServicio}";
            }
            return "Detalle Desconocido";
        }

        // Manejar click en el botón "Marcar como Reparado"
        private void btnMarcarReparado_Click(object sender, EventArgs e)
        {
            if (_presupuestoSeleccionado == 0)
            {
                MessageBox.Show("Debe seleccionar un presupuesto para marcar como reparado.", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var confirm = MessageBox.Show("¿Confirmar que la reparación fue finalizada? Se descontará el stock de repuestos.", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;

                // Descontar stock (lanza excepción si hay faltantes)
                _reparacionService.ConsumirStockYFinalizar(_presupuestoSeleccionado);

                MessageBox.Show("Reparación marcada como FINALIZADA y stock actualizado correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);

                CargarTodo();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show($"Error de stock: {ex.Message}. Verifique el estado del stock.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al finalizar la reparación: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarEsperando()
        {
            // Carga dgvEsperando con estado 'ESPERA_REPUESTOS'
            var listaEsperando = _reparacionService.GetPresupuestosPorEstado("ESPERA_REPUESTOS");
            dgvEsperando.DataSource = null;
            dgvEsperando.DataSource = listaEsperando;
        }

        private void CargarEnProceso()
        {
            // Carga dgvEnProceso con estado 'EN_PROCESO'
            var listaEnProceso = _reparacionService.GetPresupuestosPorEstado("EN_PROCESO");
            dgvEnProceso.DataSource = null;
            dgvEnProceso.DataSource = listaEnProceso;
        }


        // Carga ambas grillas
        private void CargarTodos()
        {
            CargarEsperando();
            CargarEnProceso();
        }
        // Manejar click en el botón "Refrescar"
        private void btnRefresh_Click(object sender, EventArgs e)
        {
            CargarCatalogos();
            CargarTodo();
        }
        // manejar cambio de pestaña para seleccionar primer elemento
        private void tabControlPrincipal_SelectedIndexChanged(object sender, EventArgs e)
        {
            SeleccionarPrimeraFilaSegunTab();
        }

        // manejar clicks en las grillas principales para cargar detalle
        private void dgvEsperando_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            var drv = GetSelectedRowView(dgvEsperando);
            if (drv != null) CargarSeleccionadoDesdeGrid(dgvEsperando);
        }

        private void dgvEnProceso_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            var drv = GetSelectedRowView(dgvEnProceso);
            if (drv != null) CargarSeleccionadoDesdeGrid(dgvEnProceso);
        }

        // Normaliza variantes de stock_status a valores controlados
        private string NormalizeStockStatus(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "EN_PROCESO";
            string s = raw.Trim().ToUpperInvariant().Replace(" ", "_").Replace("-", "_");

            // reglas comunes
            if (s == "ESPERANDO_REPUESTO" || s == "ESPERA_REPUESTO" || s == "ESPERA_REPUESTO S")
                s = "ESPERA_REPUESTOS";
            if (s == "REPARADO" || s == "REPARADOS")
                s = "FINALIZADO";
            if (s == "SIN_REPUESTO" || s == "NO_REPUESTOS")
                s = "SIN_REPUESTOS";

            if (s == "PENDIENTE")
                s = "EN_PROCESO";

            return s;
        }

        // Colorea filas para mejor visualización
        private void PintarFilasSegunEstado(DataGridView dgv, string estadoClave)
        {
            if (dgv == null) return;
            try
            {
                dgv.SuspendLayout();
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    if (row.IsNewRow) continue;
                    var stockNorm = "";
                    if (row.DataBoundItem is DataRowView drv && drv.Row.Table.Columns.Contains("stock_norm"))
                    {
                        stockNorm = drv["stock_norm"]?.ToString() ?? "";
                    }

                    if (estadoClave == "ESPERA_REPUESTOS" && stockNorm == "ESPERA_REPUESTOS")
                    {
                        row.DefaultCellStyle.BackColor = Color.LightSalmon;
                    }
                    else if (estadoClave == "EN_PROCESO" && (stockNorm == "EN_PROCESO" || stockNorm == "SIN_REPUESTOS"))
                    {
                        row.DefaultCellStyle.BackColor = Color.LightGreen;
                    }
                    else if (estadoClave == "FINALIZADO" && stockNorm == "FINALIZADO")
                    {
                        row.DefaultCellStyle.BackColor = Color.LightGray;
                    }
                    else
                    {
                        row.DefaultCellStyle.BackColor = Color.White;
                    }
                }
            }
            finally
            {
                dgv.ResumeLayout();
            }
        }

        private void btnProcesarReservas_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _reparacionService.ProcesarReservasDeStock();
                CargarTodo(); // recarga vista con resultados
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al procesar reservas: " + ex.Message);
            }
            finally { Cursor = Cursors.Default; }
        }
    }
}
