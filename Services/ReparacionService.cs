using CasaRepuestos.Models;
using MySql.Data.MySqlClient;
using System.Data;

namespace CasaRepuestos.Services
{
    public class ReparacionService
    {
        private readonly string _connectionString;

        public ReparacionService()
        {
            _connectionString = CasaRepuestos.Config.Config.ConnectionString;
        }

        // (Lectura) Obtiene presupuestos con su estado de stock para grillas (usado por FrmReparaciones)
        public DataTable ObtenerPresupuestosConEstadoStock()
        {
            var dt = new DataTable();

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                // Esta consulta solo lee los datos
                string sql = @"
                SELECT 
                    p.idpresupuesto, 
                    CONCAT(per.nombre, ' ', per.apellido) AS cliente,
                    p.fecha, 
                    p.estado,
                    p.estado AS stock_status, -- El estado ya es correcto
                    i.modelo, 
                    IFNULL(m.nombre, '') AS marca
                FROM presupuestos p
                INNER JOIN ingresos i ON p.idingreso = i.idingreso
                INNER JOIN clientes cl ON i.idcliente = cl.idcliente
                INNER JOIN personas per ON cl.idpersona = per.idpersona
                LEFT JOIN marcas m ON i.idmarca = m.idmarca
                WHERE p.estado IN ('PENDIENTE', 'ESPERA_REPUESTOS', 'EN_PROCESO', 'FINALIZADO') 
                  AND p.autorizado = 'SI'
                ORDER BY p.fecha ASC";

                using (var cmd = new MySqlCommand(sql, conn))
                using (var adapter = new MySqlDataAdapter(cmd))
                {
                    adapter.Fill(dt);
                }
            }
            return dt;
        }

        // (Proceso) Método principal para procesar reservas de stock según presupuestos pendientes
        public void ProcesarReservasDeStock()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();

            // Leer stock y comprometido actual
            var stock = new Dictionary<int, int>();
            var comprometido = new Dictionary<int, int>();

            string sqlStock = @"SELECT idarticulo, stock, COALESCE(stock_comprometido,0) AS sc 
                        FROM articulos";
            using (var cmd = new MySqlCommand(sqlStock, conn))
            using (var rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    int id = rd.GetInt32("idarticulo");
                    stock[id] = rd.GetInt32("stock");
                    comprometido[id] = rd.GetInt32("sc");
                }
            }

            // Traer presupuestos pendientes ordenados por fecha 
            string sqlPres = @"
        SELECT p.idpresupuesto, dp.idarticulo
        FROM presupuestos p
        JOIN detalles_presupuestos dp ON p.idpresupuesto = dp.idpresupuesto
        WHERE p.autorizado = 'SI'
          AND p.estado IN ('PENDIENTE','ESPERA_REPUESTOS')
          AND dp.idarticulo IS NOT NULL
        ORDER BY p.fecha ASC, p.idpresupuesto ASC";

            var lista = new List<(int idPres, int idArt)>();
            using (var cmd = new MySqlCommand(sqlPres, conn))
            using (var rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                    lista.Add((rd.GetInt32(0), rd.GetInt32(1)));
            }

            using var tran = conn.BeginTransaction();
            try
            {
                foreach (var (idPres, idArt) in lista)
                {
                    if (!stock.ContainsKey(idArt)) continue;

                    bool abastecido = false;

                    // Intentar usar stock_comprometido ya existente
                    if (comprometido[idArt] > 0)
                    {
                        comprometido[idArt]--;
                        abastecido = true;
                    }
                    // Si no hay comprometido, usar stock real y convertirlo a comprometido
                    else if (stock[idArt] > 0)
                    {
                        stock[idArt]--;            
                        comprometido[idArt]++;     // pasa a ser comprometido
                        ActualizarStockArticulo(idArt, conn, tran);
                        abastecido = true;
                    }

                    //  Marcar estado según si alcanzó el repuesto
                    if (abastecido)
                        ActualizarEstadoPresupuesto_DB(idPres, "EN_PROCESO", conn, tran);
                    else
                        ActualizarEstadoPresupuesto_DB(idPres, "ESPERA_REPUESTOS", conn, tran);
                }

                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }




        private void ActualizarStockArticulo(int idArt, MySqlConnection conn, MySqlTransaction tran)
        {
            string sql = @"UPDATE articulos 
                   SET stock = stock - 1, stock_comprometido = stock_comprometido + 1
                   WHERE idarticulo = @idArt";
            using var cmd = new MySqlCommand(sql, conn, tran);
            cmd.Parameters.AddWithValue("@idArt", idArt);
            cmd.ExecuteNonQuery();
        }




        private void ActualizarEstadoPresupuesto_DB(int idPres, string estado, MySqlConnection conn, MySqlTransaction tran)
        {
            string sql = @"UPDATE presupuestos 
                   SET estado = @e 
                   WHERE idpresupuesto = @id";
            using var cmd = new MySqlCommand(sql, conn, tran);
            cmd.Parameters.AddWithValue("@e", estado);
            cmd.Parameters.AddWithValue("@id", idPres);
            cmd.ExecuteNonQuery();
        }

        // Versión del método para actualizar estado que se puede usar dentro de una transacción (para evitar abrir múltiples conexiones)
        public void ActualizarEstadoPresupuesto(int idPresupuesto, string nuevoEstado, MySqlConnection conn, MySqlTransaction tran)
        {
            // Reutiliza tu método existente, pero permite pasarle la conexión y transacción
            string sql = "UPDATE presupuestos SET estado = @estado WHERE idpresupuesto = @id AND estado != @estado";
            using (var cmd = new MySqlCommand(sql, conn, tran)) // Pasa la transacción
            {
                cmd.Parameters.AddWithValue("@estado", nuevoEstado);
                cmd.Parameters.AddWithValue("@id", idPresupuesto);
                cmd.ExecuteNonQuery();
            }
        }

        // (Proceso) Método para consumir el stock comprometido y finalizar la reparación. Se llama desde el botón "Finalizar Reparación" en FrmReparaciones
        public void ConsumirStockYFinalizar(int idPresupuesto)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        // Obtener los detalles (los repuestos necesarios)
                        string sqlDetalles = @"
                            SELECT idarticulo, cantidad 
                            FROM detalles_presupuestos 
                            WHERE idpresupuesto = @idpresupuesto AND idarticulo IS NOT NULL;";

                        using var cmdDet = new MySqlCommand(sqlDetalles, conn, tran);
                        cmdDet.Parameters.AddWithValue("@idpresupuesto", idPresupuesto);
                        using var readerDet = cmdDet.ExecuteReader();

                        var detalles = new List<(int idArticulo, int cantidad)>();
                        while (readerDet.Read())
                            detalles.Add((readerDet.GetInt32("idarticulo"), readerDet.GetInt32("cantidad")));
                        readerDet.Close();

                        if (detalles.Count > 0)
                        {
                            // Consumir el stock SÓLO de 'stock_comprometido'
                            foreach (var (idArticulo, cantidadRequerida) in detalles)
                            {
                                // Bloqueamos la fila y verificamos el comprometido
                                string sqlGetStock = "SELECT COALESCE(stock_comprometido, 0) AS stock_comprometido FROM articulos WHERE idarticulo = @id FOR UPDATE;";
                                int stockComprometido = 0;

                                using (var cmdGet = new MySqlCommand(sqlGetStock, conn, tran))
                                {
                                    cmdGet.Parameters.AddWithValue("@id", idArticulo);
                                    object result = cmdGet.ExecuteScalar();
                                    if (result != null && result != DBNull.Value)
                                        stockComprometido = Convert.ToInt32(result);
                                }

                             
                                // Consume ÚNICAMENTE del stock comprometido
                                string sqlConsumir = @"
                                UPDATE articulos
                                SET 
                                stock_comprometido = GREATEST(0, stock_comprometido - @cantRequerida)
                                WHERE idarticulo = @idarticulo;";

                                using var cmdUpd = new MySqlCommand(sqlConsumir, conn, tran);
                                cmdUpd.Parameters.AddWithValue("@cantRequerida", cantidadRequerida);
                                cmdUpd.Parameters.AddWithValue("@idarticulo", idArticulo);
                                cmdUpd.ExecuteNonQuery();
                            }
                        }

                        //  Mover el presupuesto a FINALIZADO
                        string sqlUpdatePres = @"
                            UPDATE presupuestos 
                            SET estado = 'FINALIZADO', 
                                fecha = @fechaHoy 
                            WHERE idpresupuesto = @id;";

                        using var cmdPres = new MySqlCommand(sqlUpdatePres, conn, tran);
                        cmdPres.Parameters.AddWithValue("@id", idPresupuesto);
                        cmdPres.Parameters.AddWithValue("@fechaHoy", DateTime.Now);
                        cmdPres.ExecuteNonQuery();

                        tran.Commit();
                    }
                    catch (Exception ex)
                    {
                        tran.Rollback();
                        throw new Exception($"Error al finalizar la reparación {idPresupuesto}: {ex.Message}");
                    }
                }
            }
        }

        // (Proceso) Método para actualizar el estado de un presupuesto (usado por FrmReparaciones para cambiar a "EN_PROCESO", "ESPERA_REPUESTOS", etc.)
        public void ActualizarEstadoPresupuesto(int idPresupuesto, string nuevoEstado)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = "UPDATE presupuestos SET estado = @estado WHERE idpresupuesto = @id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@estado", nuevoEstado);
                    cmd.Parameters.AddWithValue("@id", idPresupuesto);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // (Lectura) Método para obtener presupuestos por estado, con detalles del cliente e ingreso (usado por FrmReparaciones para mostrar en grillas según estado)
        public List<Presupuesto> GetPresupuestosPorEstado(string estado)
        {
            var list = new List<Presupuesto>();
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
                SELECT 
                    p.idpresupuesto, p.total, p.autorizado, p.estado, p.idempleado, 
                    p.idingreso, p.fechaVencimiento, i.modelo, i.falla,
                    
                    CONCAT(per.nombre, ' ', per.apellido) AS ClienteNombre

                FROM presupuestos p
                INNER JOIN ingresos i ON p.idingreso = i.idingreso
                INNER JOIN clientes c ON i.idcliente = c.idcliente 
                
                INNER JOIN personas per ON c.idpersona = per.idpersona

                WHERE p.estado = @estado AND p.autorizado = 'SI'
                ORDER BY p.idpresupuesto DESC";

                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@estado", estado);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            list.Add(new Presupuesto
                            {
                                IdPresupuesto = rdr.GetInt32("idpresupuesto"),
                                Total = rdr.GetDecimal("total"),
                                Autorizado = rdr.GetString("autorizado"),
                                Estado = rdr.GetString("estado"),
                                IdEmpleado = rdr.GetInt32("idempleado"),
                                IdIngreso = rdr.GetInt32("idingreso"),
                                ClienteNombre = rdr.GetString("ClienteNombre"),
                                Modelo = rdr.GetString("modelo"),
                                Falla = rdr.GetString("falla"),
                                FechaVencimiento = rdr.IsDBNull(rdr.GetOrdinal("fechaVencimiento")) ? null : (DateTime?)rdr.GetDateTime("fechaVencimiento"),
                            });
                        }
                    }
                }
            }
            return list;
        }
    }
}