using CasaRepuestos.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace CasaRepuestos.Services
{
    public class PresupuestoService
    {
        private readonly string _connectionString;

        public PresupuestoService()
        {
            _connectionString = CasaRepuestos.Config.Config.ConnectionString;
        }

        #region Selects básicos (Ingresos, Servicios, Artículos, Presupuestos, Detalles)
        public List<Ingreso> GetIngresos()
        {
            var list = new List<Ingreso>();
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
                    SELECT i.idingreso, i.fecha_ingreso, i.modelo, i.falla, i.tipo_dispositivo,
                           IFNULL(m.nombre, '') AS Marca
                    FROM ingresos i
                    LEFT JOIN marcas m ON i.idmarca = m.idmarca
                    LEFT JOIN presupuestos p ON i.idingreso = p.idingreso
                    WHERE p.idingreso IS NULL
                    ORDER BY i.fecha_ingreso DESC";
                using (var cmd = new MySqlCommand(sql, conn))
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        list.Add(new Ingreso
                        {
                            IdIngreso = rdr.GetInt32("idingreso"),
                            FechaIngreso = rdr.GetDateTime("fecha_ingreso"),
                            Modelo = rdr.IsDBNull(rdr.GetOrdinal("modelo")) ? "" : rdr.GetString("modelo"),
                            Falla = rdr.IsDBNull(rdr.GetOrdinal("falla")) ? "" : rdr.GetString("falla"),
                            TipoDispositivo = Enum.TryParse<TipoDispositivo>(rdr["tipo_dispositivo"].ToString(), out var tipo) ? tipo : TipoDispositivo.SMARTPHONE,
                            Marca = rdr.IsDBNull(rdr.GetOrdinal("Marca")) ? "" : rdr.GetString("Marca")
                        });
                    }
                }
            }
            return list;
        }

        public List<Servicio> GetServicios()
        {
            var lista = new List<Servicio>();
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

               
                string sql = @"
            SELECT idservicio, descripcion_servicio, precio 
            FROM servicios 
            WHERE idarticulo IS NULL 
            ORDER BY descripcion_servicio";

                using (var cmd = new MySqlCommand(sql, conn))
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        lista.Add(new Servicio
                        {
                            IdServicio = rdr.GetInt32("idservicio"),
                            Descripcion = rdr.GetString("descripcion_servicio"),
                            Precio = rdr.GetDecimal("precio")
                        });
                    }
                }
            }
            return lista;
        }

        public List<Articulo> GetArticulos()
        {
            var lista = new List<Articulo>();
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
                    SELECT a.idarticulo, a.nombre, a.precio, stock, 
                           precioCosto, porcentajeGanancia, IVA 
                    FROM servicios
                    inner join articulos a on a.idarticulo=servicios.idarticulo 
                    WHERE idservicio IS NOT NULL
                    ORDER BY nombre";

                using (var cmd = new MySqlCommand(sql, conn))
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        lista.Add(new Articulo
                        {
                            IdArticulo = rdr.GetInt32("idarticulo"),
                            Nombre = rdr.GetString("nombre"),
                            Precio = rdr.GetDecimal("precio"),
                            Stock = rdr.GetInt32("stock"),
                            PrecioCosto = rdr.GetDecimal("precioCosto"),
                            PorcentajeGanancia = rdr.GetDecimal("porcentajeGanancia"),
                            IVA = rdr.GetDecimal("IVA")
                        });
                    }
                }
            }
            return lista;
        }

        public List<Presupuesto> GetPresupuestos()
        {
            var list = new List<Presupuesto>();
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
            SELECT 
            p.idpresupuesto,
            CONCAT(per.nombre, ' ', per.apellido) AS cliente,
            p.fecha,
            p.total,
            p.autorizado,
            p.estado,
            p.fechaVencimiento,
            p.fechaRetiro,
            p.idempleado,
            p.idingreso
        FROM presupuestos p
        INNER JOIN ingresos i ON p.idingreso = i.idingreso
        INNER JOIN clientes cl ON i.idcliente = cl.idcliente
        INNER JOIN personas per ON cl.idpersona = per.idpersona
        ORDER BY p.fecha DESC";
                using (var cmd = new MySqlCommand(sql, conn))
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        list.Add(new Presupuesto
                        {
                            IdPresupuesto = rdr.GetInt32("idpresupuesto"),
                            Fecha = rdr.GetDateTime("fecha"),
                            Total = rdr.GetDecimal("total"),
                            Autorizado = rdr.GetString("autorizado"),
                            Estado = rdr.GetString("estado"),
                            IdEmpleado = rdr.IsDBNull(rdr.GetOrdinal("idempleado")) ? 0 : rdr.GetInt32("idempleado"),
                            IdIngreso = rdr.IsDBNull(rdr.GetOrdinal("idingreso")) ? 0 : rdr.GetInt32("idingreso"),
                            FechaVencimiento = rdr.IsDBNull(rdr.GetOrdinal("fechaVencimiento"))
                             ? (DateTime?)null
                             : rdr.GetDateTime("fechaVencimiento"),
                            FechaRetiro = rdr.IsDBNull(rdr.GetOrdinal("fechaRetiro"))
                            ? (DateTime?)null
                            : rdr.GetDateTime("fechaRetiro")
                        });
                    }
                }
            }
            return list;
        }

        public List<DetallePresupuesto> GetDetallesPresupuesto(int idPresupuesto)
        {
            var detalles = new List<DetallePresupuesto>();
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                string sql = @"
            SELECT 
                dp.iddetalle_presupuesto, dp.idpresupuesto, dp.idarticulo, dp.cantidad, 
                dp.precio_repuesto, dp.precio_servicio, dp.idservicio,
                COALESCE(a.precioCosto, 0) AS precioCosto,
                COALESCE(a.porcentajeGanancia, 0) AS porcentajeGanancia,
                COALESCE(a.IVA, 0) AS IVA
                
            FROM detalles_presupuestos dp
            LEFT JOIN articulos a ON dp.idarticulo = a.idarticulo
            WHERE dp.idpresupuesto = @idPresupuesto";

                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@idPresupuesto", idPresupuesto);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            detalles.Add(new DetallePresupuesto
                            {
                                IdDetallePresupuesto = rdr.GetInt32("iddetalle_presupuesto"),
                                IdPresupuesto = rdr.GetInt32("idpresupuesto"),
                                IdArticulo = rdr.IsDBNull(rdr.GetOrdinal("idarticulo")) ? (int?)null : rdr.GetInt32("idarticulo"),
                                Cantidad = rdr.GetInt32("cantidad"),
                                PrecioRepuesto = rdr.IsDBNull(rdr.GetOrdinal("precio_repuesto")) ? (decimal?)null : rdr.GetDecimal("precio_repuesto"),
                                PrecioServicio = rdr.IsDBNull(rdr.GetOrdinal("precio_servicio")) ? (decimal?)null : rdr.GetDecimal("precio_servicio"),
                                IdServicio = rdr.IsDBNull(rdr.GetOrdinal("idservicio")) ? 0 : rdr.GetInt32("idservicio"),

                                PrecioCosto = rdr.GetDecimal("precioCosto"),
                                PorcentajeGanancia = rdr.GetDecimal("porcentajeGanancia"),
                                IVA = rdr.GetDecimal("IVA")
                            });
                        }
                    }
                }
            }
            return detalles;
        }

        #endregion

        #region Create / Update (transaccional) y UpdateAutorizado
        public int CreatePresupuesto(Presupuesto p, List<DetallePresupuesto> detalles)
        {
            if (detalles == null || !detalles.Any())
            {
                throw new Exception("El presupuesto debe tener al menos un detalle.");
            }

            if (p.Total < 0)
            {
                throw new Exception("El total no puede ser negativo.");
            }

            if (p.IdEmpleado <= 0)
            {
                throw new Exception("El empleado es obligatorio.");
            }

            if (p.IdIngreso <= 0)
            {
                throw new Exception("El ingreso es obligatorio.");
            }
            int newId = 0;
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        string sqlInsertPresupuesto = @"
                    INSERT INTO presupuestos (fecha, total, autorizado, estado, idempleado, idingreso, fechaVencimiento, fechaRetiro)
                    VALUES (@fecha, @total, @autorizado, @estado, @idempleado, @idingreso, @fechaVencimiento, @fechaRetiro)";
                        using (var cmd = new MySqlCommand(sqlInsertPresupuesto, conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@fecha", p.Fecha);
                            cmd.Parameters.AddWithValue("@total", p.Total);
                            cmd.Parameters.AddWithValue("@autorizado", string.IsNullOrEmpty(p.Autorizado) ? "NO" : p.Autorizado);
                            cmd.Parameters.AddWithValue("@estado", string.IsNullOrEmpty(p.Estado) ? "VERIFICAR_PRECIO" : p.Estado);
                            cmd.Parameters.AddWithValue("@idempleado", p.IdEmpleado);
                            cmd.Parameters.AddWithValue("@idingreso", p.IdIngreso);
                            cmd.Parameters.AddWithValue("@fechaVencimiento", (object?)p.FechaVencimiento ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@fechaRetiro", (object?)p.FechaRetiro ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                            newId = (int)cmd.LastInsertedId;
                        }

                        string sqlInsertDetalle = @"
                        INSERT INTO detalles_presupuestos (idpresupuesto, idarticulo, cantidad, precio_repuesto, precio_servicio, idservicio)
                           VALUES (@idpresupuesto, @idarticulo, @cantidad, @precio_repuesto, @precio_servicio, @idservicio)";

                        foreach (var d in detalles)
                        {
                            if (d == null) continue;
                            using (var cmd = new MySqlCommand(sqlInsertDetalle, conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@idpresupuesto", newId);
                                cmd.Parameters.AddWithValue("@idarticulo", (object?)d.IdArticulo ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@cantidad", d.Cantidad);
                                cmd.Parameters.AddWithValue("@precio_repuesto", (object?)d.PrecioRepuesto ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@precio_servicio", (object?)d.PrecioServicio ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@idservicio", d.IdServicio);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        tran.Commit();
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                }
            }
            return newId;
        }

        public void UpdatePresupuesto(Presupuesto presupuesto, List<DetallePresupuesto> detalles)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        string sqlUpdatePresupuesto = @"UPDATE presupuestos SET fecha=@fecha, total=@total, 
                    autorizado=@autorizado, estado=@estado, idempleado=@idempleado, idingreso=@idingreso,
                    fechaVencimiento=@fechaVencimiento, fechaRetiro=@fechaRetiro
                    WHERE idpresupuesto=@idpresupuesto";
                        using (var cmd = new MySqlCommand(sqlUpdatePresupuesto, conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@fecha", presupuesto.Fecha);
                            cmd.Parameters.AddWithValue("@total", presupuesto.Total);
                            cmd.Parameters.AddWithValue("@autorizado", string.IsNullOrEmpty(presupuesto.Autorizado) ? "NO" : presupuesto.Autorizado);
                            cmd.Parameters.AddWithValue("@estado", string.IsNullOrEmpty(presupuesto.Estado) ? "VERIFICAR_PRECIO" : presupuesto.Estado);
                            cmd.Parameters.AddWithValue("@idempleado", presupuesto.IdEmpleado);
                            cmd.Parameters.AddWithValue("@idingreso", presupuesto.IdIngreso);
                            cmd.Parameters.AddWithValue("@fechaVencimiento", (object?)presupuesto.FechaVencimiento ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@fechaRetiro", (object?)presupuesto.FechaRetiro ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@idpresupuesto", presupuesto.IdPresupuesto);
                            cmd.ExecuteNonQuery();
                        }

                        string sqlDeleteDetalles = "DELETE FROM detalles_presupuestos WHERE idpresupuesto=@idpresupuesto";
                        using (var cmd = new MySqlCommand(sqlDeleteDetalles, conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@idpresupuesto", presupuesto.IdPresupuesto);
                            cmd.ExecuteNonQuery();
                        }

                        string sqlInsertDetalle = @"
 INSERT INTO detalles_presupuestos (idpresupuesto, idarticulo, cantidad, precio_repuesto, precio_servicio, idservicio)
   VALUES (@idpresupuesto, @idarticulo, @cantidad, @precio_repuesto, @precio_servicio, @idservicio)";

                        foreach (var d in detalles)
                        {
                            if (d == null) continue;
                            using (var cmd = new MySqlCommand(sqlInsertDetalle, conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@idpresupuesto", presupuesto.IdPresupuesto);
                                cmd.Parameters.AddWithValue("@idarticulo", (object?)d.IdArticulo ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@cantidad", d.Cantidad);
                                cmd.Parameters.AddWithValue("@precio_repuesto", (object?)d.PrecioRepuesto ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@precio_servicio", (object?)d.PrecioServicio ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@idservicio", d.IdServicio);
                                cmd.ExecuteNonQuery();
                            }
                        }



                        tran.Commit();
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                }
            }
        }

        public void UpdateAutorizado(int idPresupuesto, string autorizado)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE presupuestos SET autorizado=@autorizado WHERE idpresupuesto=@id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@autorizado", autorizado);
                    cmd.Parameters.AddWithValue("@id", idPresupuesto);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion

        #region Métodos de Estado (limpios y explícitos)
        public void MarcarParaVerificarPrecio(int idPresupuesto)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE presupuestos SET estado = 'VERIFICAR_PRECIO' WHERE idpresupuesto = @id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", idPresupuesto);
                    cmd.ExecuteNonQuery();
                }
            }
        }

  
        public void MarcarAprobadoAdmin(int idPresupuesto)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE presupuestos SET estado = 'APROBADO_ADMIN' WHERE idpresupuesto = @id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", idPresupuesto);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public Presupuesto GetPresupuestoEstado(int idPresupuesto, MySqlConnection conn)
        {
      
            string sql = "SELECT fecha, estado FROM presupuestos WHERE idpresupuesto=@id";
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", idPresupuesto);
                using (var rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        return new Presupuesto
                        {
                            IdPresupuesto = idPresupuesto,
                 
                            Fecha = rdr.IsDBNull(0) ? (DateTime?)null : rdr.GetDateTime(0),
                            Estado = rdr.IsDBNull(1) ? "VERIFICAR_PRECIO" : rdr.GetString(1),
                        };
                    }
                }
            }
            return null;
        }

        public bool AplicarPreciosMaestros(int idPresupuesto)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                Presupuesto pres = GetPresupuestoEstado(idPresupuesto, conn);
                if (pres == null) return false;

                string estado = pres.Estado?.ToUpper() ?? "VERIFICAR_PRECIO";

                // Solo actualizamos precios si está en estos estados
                bool debeActualizar = estado == "VERIFICAR_PRECIO" || estado == "APROBADO_ADMIN";

                if (!debeActualizar)
                {
                    return false;
                }

                bool preciosCambiaron = false;

                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        string sqlArticulos = @"
                    UPDATE detalles_presupuestos dp
                    INNER JOIN articulos a ON dp.idarticulo = a.idarticulo
                    SET 
                        dp.precio_repuesto = (a.precioCosto * (1 + (a.porcentajeGanancia / 100)) * (1 + (a.IVA / 100)))
                    WHERE 
                        dp.idpresupuesto = @id AND dp.idarticulo IS NOT NULL 
                        -- Y el precio de venta calculado es diferente al que ya tiene
                        AND dp.precio_repuesto != (a.precioCosto * (1 + (a.porcentajeGanancia / 100)) * (1 + (a.IVA / 100)));";

                        using (var cmdArt = new MySqlCommand(sqlArticulos, conn, trans))
                        {
                            cmdArt.Parameters.AddWithValue("@id", idPresupuesto);
                            if (cmdArt.ExecuteNonQuery() > 0)
                            {
                                preciosCambiaron = true;
                            }
                        }

                        // Lógica de actualización de SERVICIOS 
                        string sqlServicios = @"
                    UPDATE detalles_presupuestos dp
                    INNER JOIN servicios s ON dp.idservicio = s.idservicio
                    SET dp.precio_servicio = s.precio
                    WHERE dp.idpresupuesto = @id AND dp.idservicio IS NOT NULL 
                      AND dp.idarticulo IS NULL -- 💡 Solo para servicios puros
                      AND dp.precio_servicio != s.precio;";

                        using (var cmdServ = new MySqlCommand(sqlServicios, conn, trans))
                        {
                            cmdServ.Parameters.AddWithValue("@id", idPresupuesto);
                            if (cmdServ.ExecuteNonQuery() > 0)
                            {
                                preciosCambiaron = true;
                            }
                        }

                        if (preciosCambiaron)
                        {
                            // Recalcular el Total del Presupuesto 
                            string sqlRecalcularTotal = @"
                        UPDATE presupuestos p
                        INNER JOIN (
                            SELECT idpresupuesto, 
                                   SUM((IFNULL(precio_repuesto, 0) + IFNULL(precio_servicio, 0)) * cantidad) AS nuevo_total
                            FROM detalles_presupuestos
                            WHERE idpresupuesto = @id
                            GROUP BY idpresupuesto
                        ) AS dt ON p.idpresupuesto = dt.idpresupuesto
                        SET p.total = dt.nuevo_total
                        WHERE p.idpresupuesto = @id;";

                            using (var cmdTotal = new MySqlCommand(sqlRecalcularTotal, conn, trans))
                            {
                                cmdTotal.Parameters.AddWithValue("@id", idPresupuesto);
                                cmdTotal.ExecuteNonQuery();
                            }

                            trans.Commit();
                        }
                        else
                        {
                            trans.Rollback();
                        }

                        return preciosCambiaron;
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        throw new Exception("Error al aplicar precios maestros al presupuesto: " + ex.Message);
                    }
                }
            }
        }
        public int ActualizarPreciosEnPresupuestosFlexibles(int? idArticulo, int idServicio, decimal nuevoPrecio)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                string estadosFlexibles = "'VERIFICAR_PRECIO', 'APROBADO_ADMIN'";
                int filasActualizadas = 0;

            
                string filtroBloqueo = @"
            AND NOT (
                p.estado = 'ENVIADO_A_CLIENTE' 
                AND p.fecha >= DATE_SUB(CURDATE(), INTERVAL 7 DAY)
            )";

                if (idArticulo.HasValue && idArticulo.Value > 0)
                {
                    string sql = $@"
                UPDATE detalles_presupuestos dp
                INNER JOIN presupuestos p ON dp.idpresupuesto = p.idpresupuesto
                SET dp.precio_repuesto = @nuevoPrecio
                WHERE dp.idarticulo = @idItem
                AND p.estado IN ({estadosFlexibles})
                {filtroBloqueo};";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@nuevoPrecio", nuevoPrecio);
                        cmd.Parameters.AddWithValue("@idItem", idArticulo.Value);
                        filasActualizadas = cmd.ExecuteNonQuery();
                    }
                }
                else if (idServicio > 0)
                {
                    string sql = $@"
                UPDATE detalles_presupuestos dp
                INNER JOIN presupuestos p ON dp.idpresupuesto = p.idpresupuesto
                SET dp.precio_servicio = @nuevoPrecio
                WHERE dp.idservicio = @idItem
                AND p.estado IN ({estadosFlexibles})
                {filtroBloqueo};";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@nuevoPrecio", nuevoPrecio);
                        cmd.Parameters.AddWithValue("@idItem", idServicio);
                        filasActualizadas = cmd.ExecuteNonQuery();
                    }
                }

                if (filasActualizadas > 0)
                {
                    
                    string sqlTotales = @"
                UPDATE presupuestos p
                INNER JOIN (
                    SELECT idpresupuesto, 
                           SUM((IFNULL(precio_repuesto, 0) + IFNULL(precio_servicio, 0)) * cantidad) AS nuevo_total
                    FROM detalles_presupuestos
                    GROUP BY idpresupuesto
                ) d ON p.idpresupuesto = d.idpresupuesto
                SET p.total = d.nuevo_total
                WHERE p.estado IN (" + estadosFlexibles + @");";

                    using (var cmd = new MySqlCommand(sqlTotales, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                return filasActualizadas;
            }
        }

        public void ActualizarDetallesPresupuesto(List<DetallePresupuesto> detalles)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
            UPDATE detalles_presupuestos
            SET precio_repuesto = @precioRepuesto, 
                precio_servicio = @precioServicio
            WHERE iddetalle_presupuesto = @idDetalle";

                using (var cmd = new MySqlCommand(sql, conn))
                {
                    foreach (var detalle in detalles)
                    {
                    
                        if (detalle.IdDetallePresupuesto > 0)
                        {
                            cmd.Parameters.Clear();
                           
                            cmd.Parameters.AddWithValue("@precioRepuesto", detalle.PrecioRepuesto ?? 0m);
                            cmd.Parameters.AddWithValue("@precioServicio", detalle.PrecioServicio ?? 0m);
                            cmd.Parameters.AddWithValue("@idDetalle", detalle.IdDetallePresupuesto);

                        
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }
        public void MarcarEnviadoACliente(int idPresupuesto)
        {
            var fechaEnvio = DateTime.Now.Date;
            var fechaVencimiento = fechaEnvio.AddDays(7).Date;

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
                    UPDATE presupuestos
                    SET estado = 'ENVIADO_A_CLIENTE',
                        fecha = @fechaEnvio,
                        fechaVencimiento = @fechaVencimiento,
                        autorizado = 'NO'
                    WHERE idpresupuesto = @id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", idPresupuesto);
                    cmd.Parameters.AddWithValue("@fechaEnvio", fechaEnvio);
                    cmd.Parameters.AddWithValue("@fechaVencimiento", fechaVencimiento);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void MarcarFinalizado(int idPresupuesto)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE presupuestos SET estado = 'FINALIZADO' WHERE idpresupuesto = @id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", idPresupuesto);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion

        #region Lógica automática (ciclo de 7 días y verificación)
        public void ActualizarEstadoAutomatico()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

             
                string sqlVolverAVerificar = @"
                    UPDATE presupuestos
                    SET estado = 'VERIFICAR_PRECIO'
                    WHERE estado = 'ENVIADO_A_CLIENTE'
                      AND fechaVencimiento IS NOT NULL
                      AND fechaVencimiento < CURDATE()
                      AND autorizado = 'NO'";
                using (var cmd = new MySqlCommand(sqlVolverAVerificar, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion

        #region Utilidades
        public bool EmpleadoExiste(int idEmpleado)
        {
            if (idEmpleado <= 0) return false;
            using (var cn = new MySqlConnection(_connectionString))
            {
                cn.Open();
                using (var cmd = new MySqlCommand("SELECT COUNT(1) FROM empleados WHERE idempleado = @id", cn))
                {
                    cmd.Parameters.AddWithValue("@id", idEmpleado);
                    var count = Convert.ToInt32(cmd.ExecuteScalar());
                    return count > 0;
                }
            }
        }

        public DataTable ObtenerPresupuestosAutorizados()
        {
            var dt = new DataTable();
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
            SELECT 
                p.idpresupuesto,
                CONCAT(per.nombre, ' ', per.apellido) AS cliente,
                p.fecha,
                p.estado
            FROM presupuestos p
            INNER JOIN ingresos i ON p.idingreso = i.idingreso
            INNER JOIN clientes cl ON i.idcliente = cl.idcliente
            INNER JOIN personas per ON cl.idpersona = per.idpersona
            WHERE p.autorizado = 'SI' AND p.estado <> 'FINALIZADO'
            ORDER BY p.fecha DESC;";
                using (var da = new MySqlDataAdapter(sql, conn))
                {
                    da.Fill(dt);
                }
            }
            return dt;
        }

        public Ingreso GetIngresoPorPresupuesto(int idPresupuesto)
        {
            Ingreso ingreso = null;
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = @"
            SELECT 
                i.IdIngreso, 
                i.fecha_ingreso AS FechaIngreso, 
                i.falla, 
                i.modelo AS Modelo,
                m.nombre AS Marca,
                i.tipo_dispositivo AS Tipo
            FROM presupuestos p
            INNER JOIN ingresos i ON p.idingreso = i.idingreso
            LEFT JOIN marcas m ON i.idmarca = m.idmarca
            WHERE p.idpresupuesto = @idPresupuesto;
        ";

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@idPresupuesto", idPresupuesto);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ingreso = new Ingreso
                            {
                                IdIngreso = reader.GetInt32("IdIngreso"),
                                FechaIngreso = reader.GetDateTime("FechaIngreso"),
                                Falla = reader["falla"].ToString(),
                                Marca = reader["Marca"] != DBNull.Value ? reader["Marca"].ToString() : "",
                                Modelo = reader["Modelo"] != DBNull.Value ? reader["Modelo"].ToString() : "",
                                TipoDispositivo = Enum.TryParse(reader["Tipo"]?.ToString(), true, out TipoDispositivo tipo)
                                    ? tipo
                                    : TipoDispositivo.SMARTPHONE
                            };
                        }
                    }
                }
            }
            return ingreso;
        }

        public void MarcarAutorizadoYPendiente(int idPresupuesto, string autorizado)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE presupuestos SET autorizado=@autorizado, estado='PENDIENTE' WHERE idpresupuesto=@id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@autorizado", autorizado);
                    cmd.Parameters.AddWithValue("@id", idPresupuesto);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void ActualizarTotalPresupuesto(int idPresupuesto, decimal nuevoTotal)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE presupuestos SET total=@total WHERE idpresupuesto=@id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@total", nuevoTotal);
                    cmd.Parameters.AddWithValue("@id", idPresupuesto);
                    cmd.ExecuteNonQuery();
                }
            }
        
        }
        public void ActualizarPrecioArticulo(int idArticulo, decimal nuevoPrecio)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE articulos SET precio=@precio WHERE idarticulo=@id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@precio", nuevoPrecio);
                    cmd.Parameters.AddWithValue("@id", idArticulo);
                    cmd.ExecuteNonQuery(); 
                }
            }
        }

        // Método para actualizar el precio del servicio maestro
        public void ActualizarPrecioServicio(int idServicio, decimal nuevoPrecio)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE servicios SET precio=@precio WHERE idservicio=@id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@precio", nuevoPrecio);
                    cmd.Parameters.AddWithValue("@id", idServicio);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public Presupuesto GetPresupuestoById(int idPresupuesto)
        {
            Presupuesto presupuesto = null;
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
            SELECT 
                p.idpresupuesto, p.fecha, p.total, p.autorizado, p.estado, 
                p.idempleado, p.idingreso, p.fechaVencimiento, 
                CONCAT(per.nombre, ' ', per.apellido) AS ClienteNombre 
            FROM presupuestos p
            INNER JOIN ingresos i ON p.idingreso = i.idingreso
            INNER JOIN clientes cl ON i.idcliente = cl.idcliente
            INNER JOIN personas per ON cl.idpersona = per.idpersona
            WHERE p.idpresupuesto = @id";

                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", idPresupuesto);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            presupuesto = new Presupuesto
                            {
                                IdPresupuesto = reader.GetInt32("idpresupuesto"),
                                Fecha = reader.GetDateTime("fecha"),
                                Total = reader.GetDecimal("total"),
                                Autorizado = reader["autorizado"].ToString(),
                                Estado = reader["estado"].ToString(),
                                IdEmpleado = reader.GetInt32("idempleado"),
                                IdIngreso = reader.GetInt32("idingreso"),

                                ClienteNombre = reader.GetString("ClienteNombre"),
                                FechaVencimiento = reader.IsDBNull(reader.GetOrdinal("fechaVencimiento"))
                                    ? (DateTime?)null
                                    : reader.GetDateTime("fechaVencimiento"),
                            };
                        }
                    }
                }
            }
            return presupuesto;
        }

        public string GetNombreArticulo(int idArticulo)
        {
            if (idArticulo <= 0) return "N/A";

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT nombre FROM articulos WHERE idarticulo = @id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", idArticulo);
                    var result = cmd.ExecuteScalar();
                    return result?.ToString() ?? "Artículo Desconocido";
                }
            }
        }

        public string GetDescripcionServicio(int idServicio)
        {
            if (idServicio <= 0) return "N/A";

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT descripcion_servicio FROM servicios WHERE idservicio = @id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", idServicio);
                    var result = cmd.ExecuteScalar();
                    return result?.ToString() ?? "Servicio Desconocido";
                }
            }
        }
        public int ContarPresupuestosPorVerificarPrecio()
        {
            int count = 0;
            using (var conn = new MySql.Data.MySqlClient.MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT COUNT(*) FROM presupuestos WHERE estado = 'VERIFICAR_PRECIO'";
                using (var cmd = new MySql.Data.MySqlClient.MySqlCommand(sql, conn))
                {
                    object result = cmd.ExecuteScalar();
                    count = (result == null || result == DBNull.Value) ? 0 : Convert.ToInt32(result);
                }
            }
            return count;
        }

        

        public void UpdateEstado(int idPresupuesto, string nuevoEstado)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE presupuestos SET estado=@estado WHERE idpresupuesto=@id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@estado", nuevoEstado);
                    cmd.Parameters.AddWithValue("@id", idPresupuesto);
                    cmd.ExecuteNonQuery();
                }
            }
        }
       

        #endregion

        public int? ObtenerIdPresupuestoPorIdIngreso(int idIngreso)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT idpresupuesto FROM presupuestos WHERE idingreso = @idIngreso LIMIT 1";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@idIngreso", idIngreso);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }
                }
            }
            return null;
        }

        public void RegistrarRetiroSinReparar(int idPresupuesto)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
            UPDATE presupuestos 
            SET estado = 'DEVUELTO_SIN_REPARAR',
                fechaRetiro = @fechaAhora,
                autorizado = 'NO'
            WHERE idpresupuesto = @id";

                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", idPresupuesto);
                    cmd.Parameters.AddWithValue("@fechaAhora", DateTime.Now);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public string ObtenerEstadoPresupuestoPorIdIngreso(int idIngreso)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT estado FROM presupuestos WHERE idingreso = @idIngreso LIMIT 1";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@idIngreso", idIngreso);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        return result.ToString();
                    }
                }
            }
            return null; 
        }
    }
}
