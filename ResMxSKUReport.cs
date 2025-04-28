using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite;

namespace DISMOGT_REPORTES
{
    public class ResMxSKUReportA
    {
        private readonly string _dbPath;

        public ResMxSKUReportA(string databasePath)
        {
            _dbPath = databasePath;
        }

        public List<SKUReportData> ObtenerDatos(string mfecha, string companiadm)
        {
            try
            {
                if (!File.Exists(_dbPath))
                {
                    throw new FileNotFoundException($"No se encontró la base de datos en la ruta especificada: {_dbPath}");
                }

                using (var conn = new SQLiteConnection(_dbPath))
                {
                    conn.CreateTable<SKUReportData>();

                    string fechaBuscada = mfecha;
                    string clasificacionSeleccion = "F";

                    var datosConsulta = RealizarConsulta(conn, fechaBuscada, companiadm, clasificacionSeleccion);
                    var descripcionesClasificacion = ObtenerDescripcionesClasificacion(conn, fechaBuscada, companiadm);

                    int totalClientes = ObtenerTotalClientes(conn, fechaBuscada, companiadm, clasificacionSeleccion);
                    datosConsulta.ForEach(d => d.TotalClientes = totalClientes);

                    return datosConsulta;
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"[ResMxSKUReportA] Error: Base de datos no encontrada: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxSKUReportA] Error general al obtener datos: {ex.Message}");
                throw;
            }
        }

        public List<SKUReportData> RealizarConsulta(SQLiteConnection conn, string fechaBuscada, string companiadm, string clasificacionSeleccion)
        {
            try
            {
                Console.WriteLine($"[ResMxSKUReportA] Consultando datos para fecha: {fechaBuscada}, compañía: {companiadm}, clasificación: {clasificacionSeleccion}");

                const string consulta = @"
                    SELECT 
                        PROD.COD_ART AS COD_ART,
                        DES_ART AS DESCRIPCION,
                        SUM(CNT_MAX + (CNT_MIN * 0.1)) AS UNIDADES,
                        'Q ' || ROUND(SUM((MON_TOT - DET.MON_DSC) * 1.12), 2) AS VENTA,
                        COUNT(DISTINCT COD_CLT) AS NUMERO_COBERTURAS
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
                    GROUP BY 
                        PROD.COD_ART, DES_ART
                    HAVING
                        SUM((MON_TOT - DET.MON_DSC) * 1.12) > 0
                ";

                var resultados = conn.Query<SKUReportData>(consulta, $"{fechaBuscada}%", companiadm, clasificacionSeleccion);
                Console.WriteLine($"[ResMxSKUReportA] Consulta devolvió {resultados.Count} resultados");
                return resultados;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxSKUReportA] Error al realizar consulta: {ex.Message}");
                // En caso de error, devolver una lista vacía en lugar de lanzar excepción
                return new List<SKUReportData>();
            }
        }

        public int ObtenerTotalClientes(SQLiteConnection conn, string fechaBuscada, string companiadm, string clasificacionSeleccion)
        {
            try
            {
                const string consultaTotalClientes = @"
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
                Console.WriteLine($"[ResMxSKUReportA] Error al obtener total de clientes: {ex.Message}");
                return 0;
            }
        }

        public List<string> ObtenerDescripcionesClasificacion(SQLiteConnection conn, string fechaBuscada, string companiadm)
        {
            try
            {
                Console.WriteLine($"[ResMxSKUReportA] Consultando clasificaciones para fecha: {fechaBuscada}, compañía: {companiadm}");

                string consulta = @"
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

                var clasificacionConsulta = conn.Query<ClasificacionSKU>(consulta, $"{fechaBuscada}%", companiadm);

                if (clasificacionConsulta == null || clasificacionConsulta.Count == 0)
                {
                    Console.WriteLine("[ResMxSKUReportA] No se encontraron clasificaciones con la consulta principal");

                    // Intentar con una consulta alternativa más simple
                    string consultaAlternativa = @"
                        SELECT DISTINCT
                            CLA.DESCRIPCION
                        FROM 
                            ERPADMIN_CLASIFICACION_FR CLA
                        WHERE 
                            CLA.COMPANIA = ?
                        LIMIT 20
                    ";

                    clasificacionConsulta = conn.Query<ClasificacionSKU>(consultaAlternativa, companiadm);

                    if (clasificacionConsulta != null && clasificacionConsulta.Count > 0)
                    {
                        Console.WriteLine($"[ResMxSKUReportA] Consulta alternativa encontró {clasificacionConsulta.Count} clasificaciones");
                    }
                    else
                    {
                        Console.WriteLine("[ResMxSKUReportA] No se encontraron clasificaciones con consulta alternativa");
                        return new List<string> { "No hay proveedores disponibles" };
                    }
                }

                var resultados = clasificacionConsulta.Select(c => c.DESCRIPCION).ToList();
                Console.WriteLine($"[ResMxSKUReportA] Se obtuvieron {resultados.Count} clasificaciones");
                return resultados;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResMxSKUReportA] Error al obtener descripciones de clasificación: {ex.Message}");

                // Devolver una lista con un mensaje en lugar de una lista vacía
                return new List<string> { "Error al cargar proveedores" };
            }
        }
    }

    public class SKUReportData
    {
        public string COD_ART { get; set; }
        public string DESCRIPCION_CLASIFICACION { get; set; }
        public string DESCRIPCION { get; set; }
        public double UNIDADES { get; set; }
        public string VENTA { get; set; }
        public int NUMERO_COBERTURAS { get; set; }
        public int TotalClientes { get; set; }
    }

    public class ClasificacionSKU
    {
        public string DESCRIPCION { get; set; }
    }
}