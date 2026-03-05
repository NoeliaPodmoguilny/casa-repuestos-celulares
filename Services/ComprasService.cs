using CasaRepuestos.Models;
using MySql.Data.MySqlClient;

namespace CasaRepuestos.Services
{
    public class ComprasService
    {
        private readonly ArticuloService _articuloService = new ArticuloService();
        // ====================================================================
        // SECCIÓN 1: GESTIÓN DE ÓRDENES DE COMPRA (SIN AFECTAR STOCK)
        // ====================================================================

  
        public Dictionary<Proveedor, List<ArticuloFaltante>> ObtenerArticulosFaltantesAgrupadosPorProveedor()
        {
            var resultado = new Dictionary<Proveedor, List<ArticuloFaltante>>();

            using var conn = new MySqlConnection(Config.Config.ConnectionString);
            conn.Open();

            string query = @"
            SELECT 
                prov.idproveedor,
                prov.razon_social,
                a.idarticulo,
                a.nombre AS nombre_articulo,
                ap.precioCosto,
                SUM(dp.cantidad) AS cantidad_requerida,
                a.stock AS stock_actual,
                COALESCE(a.stock_comprometido, 0) AS stock_comprometido_actual
            FROM detalles_presupuestos dp
            JOIN articulos a ON dp.idarticulo = a.idarticulo
            JOIN articulo_proveedor ap ON a.idarticulo = ap.idarticulo
            JOIN proveedores prov ON ap.idproveedor = prov.idproveedor
            JOIN presupuestos p ON dp.idpresupuesto = p.idpresupuesto
            WHERE p.estado = 'ESPERA_REPUESTOS' OR p.estado = 'EN_PROCESO'
            GROUP BY prov.idproveedor, prov.razon_social, a.idarticulo, a.nombre, ap.precioCosto, a.stock, a.stock_comprometido
            HAVING SUM(dp.cantidad) > (a.stock + COALESCE(a.stock_comprometido, 0))
        ";

            using var cmd = new MySqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var proveedor = new Proveedor
                {
                    IdProveedor = reader.GetInt32("idproveedor"),
                    RazonSocial = reader.GetString("razon_social")
                };

                int cantidadNecesaria = reader.GetInt32("cantidad_requerida") - reader.GetInt32("stock_actual");

                if (cantidadNecesaria <= 0) continue;

                var articuloFaltante = new ArticuloFaltante
                {
                    IdArticulo = reader.GetInt32("idarticulo"),
                    NombreArticulo = reader.GetString("nombre_articulo"),
                    CantidadFaltante = cantidadNecesaria,
                    PrecioUnitarioSugerido = reader.GetDecimal("precioCosto")
                };

        
                if (!resultado.Any(kvp => kvp.Key.IdProveedor == proveedor.IdProveedor))
                {
                    resultado[proveedor] = new List<ArticuloFaltante>();
                }

                var keyProveedor = resultado.Keys.First(k => k.IdProveedor == proveedor.IdProveedor);
                resultado[keyProveedor].Add(articuloFaltante);
            }

            return resultado;
        }
        private void ValidarOrdenCompra(OrdenCompra orden)
        {
            if (orden == null)
                throw new Exception("La orden no puede ser nula.");

            if (orden.IdProveedor <= 0)
                throw new Exception("Debe seleccionar un proveedor válido.");

            if (orden.FechaCreacion == default)
                throw new Exception("Debe indicar la fecha de creación de la orden.");

            if (string.IsNullOrWhiteSpace(orden.Tipo))
                throw new Exception("Debe indicar el tipo de orden.");

            if (orden.TotalEstimado <= 0)
                throw new Exception("El total estimado debe ser mayor que cero.");

            if (orden.Detalles == null || orden.Detalles.Count == 0)
                throw new Exception("Debe agregar al menos un detalle a la orden.");

            foreach (var detalle in orden.Detalles)
            {
                if (detalle.IdArticulo <= 0)
                    throw new Exception("Cada detalle debe tener un artículo válido.");
                if (detalle.CantidadSolicitada <= 0)
                    throw new Exception("La cantidad solicitada debe ser mayor que cero.");
                if (detalle.PrecioUnitarioEstimado <= 0)
                    throw new Exception("El precio estimado debe ser mayor que cero.");
            }
        }
        
        public int CrearOrdenDeCompra(OrdenCompra orden)
        {
            ValidarOrdenCompra(orden);
            using var conn = new MySqlConnection(Config.Config.ConnectionString);
            conn.Open();
            using var tran = conn.BeginTransaction();

            try
            {

                var insertOrden = @"
                INSERT INTO ordenes_compra (fecha_emision, total_estimado, idproveedor, estado, tipo)
                VALUES (@fecha, @total, @idproveedor, 'PENDIENTE', @tipo);
                SELECT LAST_INSERT_ID();"; 

                using var cmdOrden = new MySqlCommand(insertOrden, conn, tran);
                cmdOrden.Parameters.AddWithValue("@fecha", orden.FechaCreacion);
                cmdOrden.Parameters.AddWithValue("@total", orden.TotalEstimado);
                cmdOrden.Parameters.AddWithValue("@idproveedor", orden.IdProveedor);
                cmdOrden.Parameters.AddWithValue("@tipo", orden.Tipo);

                int idOrden = Convert.ToInt32(cmdOrden.ExecuteScalar());
                foreach (var det in orden.Detalles)
                {
                    var insertDet = @"
                        INSERT INTO detalles_orden_compra (idorden_compra, idarticulo, cantidad_solicitada, precio_unitario_estimado)
                        VALUES (@idorden, @idarticulo, @cantidad, @precio);";
                    using var cmdDet = new MySqlCommand(insertDet, conn, tran);
                    cmdDet.Parameters.AddWithValue("@idorden", idOrden);
                    cmdDet.Parameters.AddWithValue("@idarticulo", det.IdArticulo);
                    cmdDet.Parameters.AddWithValue("@cantidad", det.CantidadSolicitada);
                    cmdDet.Parameters.AddWithValue("@precio", det.PrecioUnitarioEstimado);
                    cmdDet.ExecuteNonQuery();
                }

                tran.Commit();
                return idOrden;
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

   
        public List<OrdenCompra> ListarOrdenesDeCompraPendientes()
        {
            var lista = new List<OrdenCompra>();
            using var conn = new MySqlConnection(Config.Config.ConnectionString);
            conn.Open();

            var query = @"
                SELECT oc.idorden_compra, oc.fecha_emision, oc.total_estimado, oc.estado, p.razon_social
                FROM ordenes_compra oc
                JOIN proveedores p ON oc.idproveedor = p.idproveedor
                WHERE oc.estado = 'PENDIENTE'
                ORDER BY oc.idorden_compra DESC";

            using var cmd = new MySqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add(new OrdenCompra
                {
                    IdOrdenCompra = reader.GetInt32("idorden_compra"),
                    FechaCreacion= reader.GetDateTime("fecha_emision"),
                    TotalEstimado = reader.GetDecimal("total_estimado"),
                    Estado = reader.GetString("estado"),
                    ProveedorNombre = reader.GetString("razon_social")
                });
            }
            return lista;
        }



        public OrdenCompra GetOrdenConDetalles(int idOrdenCompra)
        {
            OrdenCompra orden = null;
            using var conn = new MySqlConnection(Config.Config.ConnectionString);
            conn.Open();

            var queryOrden = @"
            SELECT oc.idorden_compra, oc.fecha_emision, oc.total_estimado, oc.idproveedor, oc.estado, oc.tipo, p.razon_social
            FROM ordenes_compra oc
            JOIN proveedores p ON oc.idproveedor = p.idproveedor
            WHERE oc.idorden_compra = @id";

            using var cmdOrden = new MySqlCommand(queryOrden, conn);
            cmdOrden.Parameters.AddWithValue("@id", idOrdenCompra);
            using var readerOrden = cmdOrden.ExecuteReader();

            if (readerOrden.Read())
            {
                orden = new OrdenCompra
                {
                    IdOrdenCompra = readerOrden.GetInt32("idorden_compra"),
                    FechaCreacion = readerOrden.GetDateTime("fecha_emision"),
                    TotalEstimado = readerOrden.GetDecimal("total_estimado"),
                    IdProveedor = readerOrden.GetInt32("idproveedor"),
                    Estado = readerOrden.GetString("estado"),
                    Tipo = readerOrden.GetString("tipo"),
                    ProveedorNombre = readerOrden.GetString("razon_social") 
                };
            }
            readerOrden.Close();

            if (orden != null)
            {
                var queryDetalles = @"
            SELECT d.idarticulo, a.nombre, d.cantidad_solicitada, d.precio_unitario_estimado
            FROM detalles_orden_compra d
            JOIN articulos a ON d.idarticulo = a.idarticulo
            WHERE d.idorden_compra = @id";
                using var cmdDetalles = new MySqlCommand(queryDetalles, conn);
                cmdDetalles.Parameters.AddWithValue("@id", idOrdenCompra);
                using var readerDetalles = cmdDetalles.ExecuteReader();

                while (readerDetalles.Read())
                {
                    orden.Detalles.Add(new DetalleOrdenCompra
                    {
                        IdArticulo = readerDetalles.GetInt32("idarticulo"),
                        ArticuloNombre = readerDetalles.GetString("nombre"),
                        CantidadSolicitada = readerDetalles.GetInt32("cantidad_solicitada"),
                        PrecioUnitarioEstimado = readerDetalles.GetDecimal("precio_unitario_estimado")
                    });
                }
            }



            return orden;
        }
 
        public List<OrdenCompra> ListarOrdenesPendientes()
        {
            var lista = new List<OrdenCompra>();

            using var conn = new MySqlConnection(Config.Config.ConnectionString);
            conn.Open();

            string query = @"
                SELECT 
                    oc.idorden_compra, oc.fecha_emision, oc.total_estimado, oc.tipo, oc.estado,
                    p.idproveedor, p.razon_social
                FROM ordenes_compra oc
                JOIN proveedores p ON oc.idproveedor = p.idproveedor
                WHERE oc.estado = 'PENDIENTE' 
                ORDER BY oc.fecha_emision DESC";

            using var cmd = new MySqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add(new OrdenCompra
                {
                    IdOrdenCompra = reader.GetInt32("idorden_compra"),
                    FechaCreacion = reader.GetDateTime("fecha_emision"),
                    TotalEstimado = reader.GetDecimal("total_estimado"),
                    Tipo = reader.GetString("tipo"),
                    Estado = reader.GetString("estado"),
                    IdProveedor = reader.GetInt32("idproveedor"),
                    ProveedorNombre = reader.GetString("razon_social") 
                });
            }

            return lista;
        }
        // ====================================================================
        // SECCIÓN 2: GESTIÓN DE COMPRAS 
        // ====================================================================
        // Registra la recepción de una orden de compra, crea la Compra final y ACTUALIZA EL STOCK

        public int RegistrarIngresoDesdeOrden(Compra compraFinal)
        {
            ValidarCompra(compraFinal);
            using var conn = new MySqlConnection(Config.Config.ConnectionString);
            conn.Open();
            using var tran = conn.BeginTransaction();
            int idCompra = 0;

            try
            {
                var insertCompra = @"
                INSERT INTO compras (fecha, total, idproveedor, tipo, idorden_compra, metodo_pago)
                VALUES (@fecha, @total, @idproveedor, @tipo, @idorden, @metodoPago);";
                 using (var cmdCompra = new MySqlCommand(insertCompra, conn, tran))
                {
                    cmdCompra.Parameters.AddWithValue("@fecha", compraFinal.Fecha);
                    cmdCompra.Parameters.AddWithValue("@total", compraFinal.Total);
                    cmdCompra.Parameters.AddWithValue("@idproveedor", compraFinal.IdProveedor);
                    cmdCompra.Parameters.AddWithValue("@tipo", compraFinal.Tipo);
                    cmdCompra.Parameters.AddWithValue("@idorden", (object)compraFinal.IdOrdenCompra ?? DBNull.Value);
                    cmdCompra.Parameters.AddWithValue("@metodoPago", compraFinal.MetodoPago);
                    cmdCompra.ExecuteNonQuery();
                }

                idCompra = Convert.ToInt32(new MySqlCommand("SELECT LAST_INSERT_ID();", conn, tran).ExecuteScalar());

                foreach (var det in compraFinal.Detalles)
                {
                    var insertDet = @"
                    INSERT INTO detalles_compras (cantidad, precio_unitario, idcompra, idarticulo)
                    VALUES (@cant, @precio, @idcompra, @idarticulo);";
                    using (var cmdDet = new MySqlCommand(insertDet, conn, tran))
                    {
                        cmdDet.Parameters.AddWithValue("@cant", det.Cantidad);
                        cmdDet.Parameters.AddWithValue("@precio", det.PrecioUnitario); 
                        cmdDet.Parameters.AddWithValue("@idcompra", idCompra);
                        cmdDet.Parameters.AddWithValue("@idarticulo", det.IdArticulo);
                        cmdDet.ExecuteNonQuery();
                    }

                    if (compraFinal.Tipo != null && compraFinal.Tipo.Trim().ToUpper() == "REPUESTO")
                    {
                        string updateComprometido = @"
                        UPDATE articulos
                        SET stock_comprometido = COALESCE(stock_comprometido, 0) + @cant
                        WHERE idarticulo = @idarticulo;";
                        using (var cmd = new MySqlCommand(updateComprometido, conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@cant", det.Cantidad);
                            cmd.Parameters.AddWithValue("@idarticulo", det.IdArticulo);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        string updateStock = @"
                        UPDATE articulos 
                        SET stock = stock + @cant
                        WHERE idarticulo = @idarticulo;";
                        using (var cmd = new MySqlCommand(updateStock, conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@cant", det.Cantidad);
                            cmd.Parameters.AddWithValue("@idarticulo", det.IdArticulo);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    string updateCostoProveedor = @"
                        UPDATE articulo_proveedor 
                        SET precioCoste = @precioFinal 
                        WHERE idarticulo = @idarticulo AND idproveedor = @idproveedor";
                    using (var cmdCosto = new MySqlCommand(updateCostoProveedor, conn, tran))
                    {
                        cmdCosto.Parameters.AddWithValue("@precioFinal", det.PrecioUnitario);
                        cmdCosto.Parameters.AddWithValue("@idarticulo", det.IdArticulo);
                        cmdCosto.Parameters.AddWithValue("@idproveedor", compraFinal.IdProveedor);
                        cmdCosto.ExecuteNonQuery();
                    }

                    _articuloService.ActualizarPrecioCostoArticulo(det.IdArticulo, conn, tran);

                } 

                if (compraFinal.IdOrdenCompra.HasValue)
                {
                    string updateOrden = "UPDATE ordenes_compra SET estado = 'RECIBIDA' WHERE idorden_compra = @id;";
                    using var cmdUpdateOrden = new MySqlCommand(updateOrden, conn, tran);
                    cmdUpdateOrden.Parameters.AddWithValue("@id", compraFinal.IdOrdenCompra.Value);
                    cmdUpdateOrden.ExecuteNonQuery();
                }
                
                tran.Commit();
                return idCompra;
            }
            catch (Exception ex)
            {
                tran.Rollback();
                throw new Exception($"Error en RegistrarIngresoDesdeOrden: {ex.Message}", ex);
            }
        }

        private void ValidarCompra(Compra compra)
        {
            if (compra.IdProveedor <= 0)
                throw new Exception("Debe seleccionar un proveedor.");

            
            if (compra.Total < 0)
                throw new Exception("El total de la compra no puede ser negativo.");

            if (compra.Fecha == default)
                throw new Exception("La fecha de la compra es obligatoria.");

            if (string.IsNullOrWhiteSpace(compra.Tipo))
                throw new Exception("Debe especificar el tipo de compra.");

            if (compra.Total > 0 && (compra.Detalles == null || compra.Detalles.Count == 0))
                throw new Exception("Debe agregar al menos un detalle a la compra si el total es mayor a cero.");

            // Validamos los detalles solo si existen
            if (compra.Detalles != null)
            {
                foreach (var d in compra.Detalles)
                {
                    if (d.IdArticulo <= 0)
                        throw new Exception("Cada detalle debe tener un artículo válido.");

                    if (d.Cantidad <= 0)
                        throw new Exception("La cantidad debe ser mayor a cero.");

                    if (d.PrecioUnitario < 0)
                        throw new Exception("El precio unitario no puede ser negativo.");
                }
            }
        }
        
        public List<dynamic> ListarHistorialCompras(string tipo = "", DateTime? desde = null, DateTime? hasta = null)
        {
            var lista = new List<dynamic>();
            using var conn = new MySqlConnection(Config.Config.ConnectionString);
            conn.Open();

            var query = new System.Text.StringBuilder(@"
                SELECT 
                    c.idcompra,
                    c.fecha,
                    c.total,
                    c.tipo,
                    p.razon_social AS Proveedor,
                    GROUP_CONCAT(CONCAT(a.nombre, ' (', dc.cantidad, ')') SEPARATOR ', ') AS ArticulosResumen
                FROM compras c
                JOIN proveedores p ON c.idproveedor = p.idproveedor
                JOIN detalles_compras dc ON c.idcompra = dc.idcompra
                JOIN articulos a ON dc.idarticulo = a.idarticulo
                WHERE 1=1 ");

            // Construcción dinámica de los filtros
            if (!string.IsNullOrEmpty(tipo))
            {
                query.Append(" AND TRIM(UPPER(c.tipo)) = @tipo");
            }
            if (desde.HasValue)
            {
                query.Append(" AND c.fecha >= @desde");
            }
            if (hasta.HasValue)
            {
                // Asegura que la fecha final incluya todo el día
                query.Append(" AND c.fecha < @hastaFinDia");
            }

            query.Append(@" 
                GROUP BY c.idcompra, c.fecha, c.total, c.tipo, p.razon_social
                ORDER BY c.fecha DESC, c.idcompra DESC");

            using var cmd = new MySqlCommand(query.ToString(), conn);

            // Asignar los valores a los parámetros
            if (!string.IsNullOrEmpty(tipo))
            {
                cmd.Parameters.AddWithValue("@tipo", tipo.Trim().ToUpper());
            }
            if (desde.HasValue)
            {
                cmd.Parameters.AddWithValue("@desde", desde.Value.Date);
            }
            if (hasta.HasValue)
            {
                // Pasamos el día siguiente 
                cmd.Parameters.AddWithValue("@hastaFinDia", hasta.Value.Date.AddDays(1));
            }

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add(new
                {
                    IdCompra = reader.GetInt32("idcompra"),
                    Fecha = reader.GetDateTime("fecha"),
                    Total = reader.IsDBNull(reader.GetOrdinal("total")) ? 0.0 : reader.GetDouble("total"),
                    Tipo = reader.GetString("tipo"),
                    Proveedor = reader.IsDBNull(reader.GetOrdinal("Proveedor")) ? "N/A" : reader.GetString("Proveedor"),
                    ArticulosResumen = reader.IsDBNull(reader.GetOrdinal("ArticulosResumen")) ? "Sin Artículos" : reader.GetString("ArticulosResumen")
                });
            }
            return lista;
        }

        public List<ArticuloFaltante> ObtenerNecesidadesRepuestosConsolidadas()
        {
            var resultado = new List<ArticuloFaltante>();

            using var conn = new MySqlConnection(Config.Config.ConnectionString);
            conn.Open();

            string query = @"
                SELECT 
                    dp.idarticulo,
                    a.nombre AS nombre_articulo,
                    a.precioCosto AS precio_unitario_sugerido,
                    (SUM(dp.cantidad) - (COALESCE(a.stock, 0) + COALESCE(a.stock_comprometido, 0))) AS cantidad_faltante
    
                    FROM detalles_presupuestos dp
                    INNER JOIN articulos a ON dp.idarticulo = a.idarticulo
                    INNER JOIN presupuestos p ON dp.idpresupuesto = p.idpresupuesto
                    WHERE
   
                    p.estado IN ('ESPERA_REPUESTOS', 'EN_PROCESO') AND  
                    dp.idarticulo IS NOT NULL          
                    GROUP BY 
                      dp.idarticulo, a.nombre, a.precioCosto, a.stock, a.stock_comprometido -- Añadido stock_comprometido al GROUP BY
                    HAVING (SUM(dp.cantidad) - (COALESCE(a.stock, 0) + COALESCE(a.stock_comprometido, 0))) > 0
                    ORDER BY a.nombre;
                        ";

            using var cmd = new MySqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                resultado.Add(new ArticuloFaltante
                {
                    IdArticulo = reader.GetInt32("idarticulo"),
                    NombreArticulo = reader.GetString("nombre_articulo"),
                    CantidadFaltante = reader.GetInt32("cantidad_faltante"),
                    PrecioUnitarioSugerido = reader.GetDecimal("precio_unitario_sugerido"),
                    IdProveedor = 0,
                    ProveedorNombre = ""
                });
            }

            return resultado;
        }

        public void ActualizarPrecioCostoProveedor(int idArticulo, int idProveedor, decimal nuevoPrecioCosto)
        {
            

            using var conn = new MySqlConnection(Config.Config.ConnectionString);
            conn.Open();
            using var tran = conn.BeginTransaction();

            try
            {
                string updateCostoProveedor = @"
            UPDATE articulo_proveedor 
            SET precioCoste = @precioFinal 
            WHERE idarticulo = @idarticulo AND idproveedor = @idproveedor";
                using (var cmdCosto = new MySqlCommand(updateCostoProveedor, conn, tran))
                {
                    cmdCosto.Parameters.AddWithValue("@precioFinal", nuevoPrecioCosto);
                    cmdCosto.Parameters.AddWithValue("@idarticulo", idArticulo);
                    cmdCosto.Parameters.AddWithValue("@idproveedor", idProveedor);

                     
                    if (cmdCosto.ExecuteNonQuery() == 0)
                    {
                        tran.Rollback();
                        return;
                    }
                }

                _articuloService.ActualizarPrecioCostoArticulo(idArticulo, conn, tran);

                tran.Commit();
            }
            catch (Exception ex)
            {
                tran.Rollback();
                throw new Exception($"Error al actualizar precioCosto del proveedor: {ex.Message}", ex);
            }
        }
        public bool PresupuestoRequiereCompra(int idPresupuesto)
        {
          
            var query = @"
        SELECT 1
        FROM detalles_presupuestos dp
        INNER JOIN articulos a ON dp.idarticulo = a.idarticulo
        WHERE dp.idpresupuesto = @idpresupuesto 
          AND dp.idarticulo IS NOT NULL
          AND a.stock < dp.cantidad 
        LIMIT 1;
    ";

            using var conn = new MySqlConnection(CasaRepuestos.Config.Config.ConnectionString);
            conn.Open();
            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@idpresupuesto", idPresupuesto);

            return cmd.ExecuteScalar() != null;
        }
        public int ContarPresupuestosEnEsperaDeRepuestos()
        {
            int count = 0;
            using (var conn = new MySqlConnection(CasaRepuestos.Config.Config.ConnectionString))
            {
                conn.Open();
                string sql = "SELECT COUNT(*) FROM presupuestos WHERE estado = 'ESPERA_REPUESTOS'";

                using (var cmd = new MySqlCommand(sql, conn))
                {
                    object result = cmd.ExecuteScalar();
                    count = (result == null || result == DBNull.Value) ? 0 : Convert.ToInt32(result);
                }
            }
            return count;
        }


        
      
    }


}

