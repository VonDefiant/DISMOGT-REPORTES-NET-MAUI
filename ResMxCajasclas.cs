using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite;

namespace DISMOGT_REPORTES
{
    public class ResMxCajasclasA
    {
        private string dbPath;

        public ResMxCajasclasA(string databasePath)
        {
            dbPath = databasePath;
        }

        public List<CAJASReportData> ObtenerDatos(string mfecha, string companiadm)
        {
            try
            {
                // Comprobar que la base de datos principal existe
                if (!File.Exists(dbPath))
                {
                    throw new FileNotFoundException($"No se encontró la base de datos principal en la ruta: {dbPath}");
                }

                // Comprobar que la base de datos de UXCDISMOGT existe
                string uxcDbPath = "/storage/emulated/0/DISMOGTREPORTES/UXCDISMOGT.db";
                if (!File.Exists(uxcDbPath))
                {
                    Console.WriteLine($"ADVERTENCIA: No se encontró la base de datos UXC en la ruta: {uxcDbPath}");
                    Console.WriteLine("Intentando buscar en ubicaciones alternativas...");

                    // Intentar ubicaciones alternativas
                    string[] alternativePaths = new string[] {
                        "/storage/emulated/0/Android/data/com.dismogt.app/files/UXCDISMOGT.db",
                        "/storage/emulated/0/Download/UXCDISMOGT.db",
                        "/data/data/com.dismogt.app/files/UXCDISMOGT.db"
                    };

                    foreach (string path in alternativePaths)
                    {
                        if (File.Exists(path))
                        {
                            uxcDbPath = path;
                            Console.WriteLine($"¡Base de datos UXC encontrada en ubicación alternativa: {uxcDbPath}!");
                            break;
                        }
                    }

                    if (!File.Exists(uxcDbPath))
                    {
                        // Si aún no encuentra, lanzar excepción
                        throw new FileNotFoundException($"No se pudo encontrar la base de datos UXCDISMOGT.db. Por favor, descargue los datos desde el menú principal.");
                    }
                }

                string fechaBuscada = mfecha;
                string clasificacionSeleccion = "F";

                using (SQLiteConnection conn = new SQLiteConnection(dbPath))
                {
                    try
                    {
                        Console.WriteLine("Abriendo conexión a base de datos principal...");

                        // Verificar estado de la conexión
                        if (conn.Handle == null)
                        {
                            // No usar SQLiteException directamente con parámetros
                            throw new Exception("No se pudo abrir la conexión a la base de datos principal");
                        }

                        var datosConsulta = RealizarConsulta(conn, fechaBuscada, companiadm, clasificacionSeleccion, uxcDbPath);
                        var descripcionesClasificacion = ObtenerDescripcionesClasificacion(conn, fechaBuscada, companiadm);
                        var totalClientes = ObtenerTotalClientes(conn, fechaBuscada, companiadm, clasificacionSeleccion);

                        datosConsulta.ForEach(d => d.TotalClientes = totalClientes);

                        return datosConsulta;
                    }
                    catch (SQLiteException sqlEx)
                    {
                        Console.WriteLine($"Error de SQLite: {sqlEx.Message}");
                        throw;
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"Error de archivo no encontrado: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error general: {ex.Message}");
                throw;
            }
        }

        public List<CAJASReportData> RealizarConsulta(SQLiteConnection conn, string fechaBuscada, string companiadm, string clasificacionSeleccion, string uxcDbPath)
        {
            try
            {
                Console.WriteLine("Verificando si la base de datos UXC está adjunta...");

                // Verificar y limpiar cualquier conexión previa
                try
                {
                    // Intentar desconectar primero por si hay una conexión existente
                    conn.Execute("DETACH DATABASE IF EXISTS UXCDISMOGT;");
                    Console.WriteLine("Base de datos UXCDISMOGT desconectada correctamente (si existía)");
                }
                catch (SQLiteException ex)
                {
                    Console.WriteLine($"Aviso al desconectar base de datos: {ex.Message}");
                    // Continuar el proceso, no es un error crítico
                }

                // Adjuntar la base de datos UXC
                string attachQuery = $"ATTACH DATABASE '{uxcDbPath}' AS UXCDISMOGT;";
                conn.Execute(attachQuery);
                Console.WriteLine($"Base de datos UXC adjuntada desde: {uxcDbPath}");

                // Verificar que la tabla UXC exista en la base de datos adjunta
                bool tablasVerificadas = false;
                try
                {
                    var tableCheck = conn.Query<TablaInfo>("SELECT * FROM UXCDISMOGT.sqlite_master WHERE type='table' AND name='UXC' LIMIT 1;");
                    if (tableCheck == null || tableCheck.Count == 0)
                    {
                        Console.WriteLine("¡La tabla UXC no existe en la base de datos adjunta!");
                        // Continuar con consulta alternativa sin usar la tabla UXC
                    }
                    else
                    {
                        Console.WriteLine("Tabla UXC verificada correctamente");
                        tablasVerificadas = true;
                    }
                }
                catch (SQLiteException ex)
                {
                    Console.WriteLine($"Error al verificar tablas: {ex.Message}");
                    // Continuar con consulta alternativa
                }

                List<CAJASReportData> datosConsulta;

                if (tablasVerificadas)
                {
                    // Usar consulta con tabla UXC
                    string consulta = @"
                        SELECT 
                            PROD.COD_CLS,
                            CLA_DESC.DESCRIPCION AS AGRUPACION,
                            SUM(CNT_MAX + (CNT_MIN * 0.1)) AS UNIDADES,
                            'Q ' || ROUND(SUM((MON_TOT - DET.MON_DSC) * 1.12), 2) AS VENTA,
                            COUNT(DISTINCT COD_CLT) AS NUMERO_COBERTURAS,
                            ROUND(SUM((DET.CNT_MAX + (DET.CNT_MIN * 0.1)) / COALESCE(UXC.FACTOR_CONVER_6, 1)), 2) AS CAJAS
                        FROM 
                            ERPADMIN_ALFAC_DET_PED DET
                        JOIN 
                            ERPADMIN_ALFAC_ENC_PED ENC ON DET.NUM_PED = ENC.NUM_PED
                        JOIN 
                            ERPADMIN_ARTICULO PROD ON PROD.COD_ART = DET.COD_ART
                        JOIN 
                            ERPADMIN_CLASIFICACION_FR CLA ON SUBSTR(PROD.COD_FAM, 1, 2) = CLA.CLASIFICACION
                        JOIN 
                            ERPADMIN_CLASIFICACION_FR CLA_DESC ON PROD.COD_CLS = CLA_DESC.CLASIFICACION
                        LEFT JOIN 
                            UXCDISMOGT.UXC ON PROD.COD_ART = UXC.ARTICULO
                        WHERE 
                            ENC.ESTADO <> 'C' AND ENC.FEC_PED LIKE ? || '%'
                            AND CLA.COMPANIA = ?
                            AND CLA.DESCRIPCION LIKE ?
                        GROUP BY 
                            PROD.COD_CLS, CLA_DESC.DESCRIPCION
                        HAVING
                            SUM((MON_TOT - DET.MON_DSC) * 1.12) > 0;
                    ";

                    datosConsulta = conn.Query<CAJASReportData>(consulta, $"{fechaBuscada}%", companiadm, clasificacionSeleccion);
                }
                else
                {
                    // Usar consulta alternativa sin tabla UXC
                    string consultaAlternativa = @"
                        SELECT 
                            PROD.COD_CLS,
                            CLA_DESC.DESCRIPCION AS AGRUPACION,
                            SUM(CNT_MAX + (CNT_MIN * 0.1)) AS UNIDADES,
                            'Q ' || ROUND(SUM((MON_TOT - DET.MON_DSC) * 1.12), 2) AS VENTA,
                            COUNT(DISTINCT COD_CLT) AS NUMERO_COBERTURAS,
                            ROUND(SUM((DET.CNT_MAX + (DET.CNT_MIN * 0.1)) / 1), 2) AS CAJAS
                        FROM 
                            ERPADMIN_ALFAC_DET_PED DET
                        JOIN 
                            ERPADMIN_ALFAC_ENC_PED ENC ON DET.NUM_PED = ENC.NUM_PED
                        JOIN 
                            ERPADMIN_ARTICULO PROD ON PROD.COD_ART = DET.COD_ART
                        JOIN 
                            ERPADMIN_CLASIFICACION_FR CLA ON SUBSTR(PROD.COD_FAM, 1, 2) = CLA.CLASIFICACION
                        JOIN 
                            ERPADMIN_CLASIFICACION_FR CLA_DESC ON PROD.COD_CLS = CLA_DESC.CLASIFICACION
                        WHERE 
                            ENC.ESTADO <> 'C' AND ENC.FEC_PED LIKE ? || '%'
                            AND CLA.COMPANIA = ?
                            AND CLA.DESCRIPCION LIKE ?
                        GROUP BY 
                            PROD.COD_CLS, CLA_DESC.DESCRIPCION
                        HAVING
                            SUM((MON_TOT - DET.MON_DSC) * 1.12) > 0;
                    ";

                    datosConsulta = conn.Query<CAJASReportData>(consultaAlternativa, $"{fechaBuscada}%", companiadm, clasificacionSeleccion);
                    Console.WriteLine("Usando consulta alternativa sin la tabla UXC");
                }

                // Desconectar la base de datos adjunta
                try
                {
                    conn.Execute("DETACH DATABASE UXCDISMOGT;");
                    Console.WriteLine("Base de datos UXCDISMOGT desconectada correctamente");
                }
                catch (SQLiteException ex)
                {
                    Console.WriteLine($"Aviso al desconectar base de datos al finalizar: {ex.Message}");
                    // No es un error crítico
                }

                return datosConsulta;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al realizar la consulta: {ex.Message}");
                throw;
            }
        }

        public int ObtenerTotalClientes(SQLiteConnection conn, string fechaBuscada, string companiadm, string clasificacionSeleccion)
        {
            try
            {
                string consultaTotalClientes = $@"
                    SELECT 
                        COUNT(DISTINCT COD_CLT) AS TotalClientes
                    FROM 
                        ERPADMIN_ALFAC_DET_PED DET
                    JOIN 
                        ERPADMIN_ALFAC_ENC_PED ENC ON DET.NUM_PED = ENC.NUM_PED
                    JOIN 
                        ERPADMIN_ARTICULO PROD ON PROD.COD_ART = DET.COD_ART
                    JOIN 
                        ERPADMIN_CLASIFICACION_FR CLA ON SUBSTR(PROD.COD_FAM, 1, 2) = CLA.CLASIFICACION
                    WHERE 
                        ESTADO <> 'C' AND FEC_PED LIKE ? || '%'
                        AND COMPANIA = ?
                        AND CLA.DESCRIPCION LIKE ?
                        AND (DET.MON_TOT - DET.MON_DSC) * 1.12 > 0;
                ";

                var totalClientes = conn.ExecuteScalar<int>(consultaTotalClientes, $"{fechaBuscada}%", companiadm, clasificacionSeleccion);
                return totalClientes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener total de clientes: {ex.Message}");
                return 0; // Valor por defecto en caso de error
            }
        }

        public static string ObtenerConsultaClasificacion()
        {
            return $@"
                SELECT 
                    CLA.DESCRIPCION
                FROM 
                    ERPADMIN_ALFAC_DET_PED DET
                JOIN 
                    ERPADMIN_ALFAC_ENC_PED ENC ON DET.NUM_PED = ENC.NUM_PED
                JOIN 
                    ERPADMIN_ARTICULO PROD ON PROD.COD_ART = DET.COD_ART
                JOIN 
                    ERPADMIN_CLASIFICACION_FR CLA ON SUBSTR(PROD.COD_FAM, 1, 2) = CLA.CLASIFICACION
                WHERE 
                    ESTADO <> 'C' AND FEC_PED LIKE ? || '%'
                    AND COMPANIA = ?
                GROUP BY 
                    CLA.DESCRIPCION
                HAVING
                    SUM((MON_TOT - DET.MON_DSC) * 1.12) > 0
            ";
        }

        public List<string> ObtenerDescripcionesClasificacion(SQLiteConnection conn, string fechaBuscada, string companiadm)
        {
            try
            {
                string consultaClasificacion = ObtenerConsultaClasificacion();

                var clasificacionConsulta = conn.Query<ClasificacionDataCAJAS>(consultaClasificacion, $"{fechaBuscada}%", companiadm);

                var opcionesClasificacion = clasificacionConsulta.Select(c => c.DESCRIPCION).ToList();
                return opcionesClasificacion;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener descripciones de clasificación: {ex.Message}");
                return new List<string>(); // Lista vacía en caso de error
            }
        }
    }

    public class CAJASReportData
    {
        public string COD_CLS { get; set; }
        public string AGRUPACION { get; set; }
        public string UNIDADES { get; set; }
        public string VENTA { get; set; }
        public string NUMERO_COBERTURAS { get; set; }
        public double CAJAS { get; set; }
        public int TotalClientes { get; set; }
    }

    public class ClasificacionDataCAJAS
    {
        public string DESCRIPCION { get; set; }
    }

    public class TablaInfo
    {
        public string name { get; set; }
        public string type { get; set; }
    }
}