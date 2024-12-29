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
                throw new FileNotFoundException($"No se encontró la base de datos en la ruta especificada: {_dbPath}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener datos: {ex.Message}", ex);
            }
        }

        public List<SKUReportData> RealizarConsulta(SQLiteConnection conn, string fechaBuscada, string companiadm, string clasificacionSeleccion)
        {
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

            return conn.Query<SKUReportData>(consulta, $"{fechaBuscada}%", companiadm, clasificacionSeleccion);
        }

        public int ObtenerTotalClientes(SQLiteConnection conn, string fechaBuscada, string companiadm, string clasificacionSeleccion)
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

            return conn.ExecuteScalar<int>(consultaTotalClientes, $"{fechaBuscada}%", companiadm, clasificacionSeleccion);
        }

        public static string ObtenerConsultaClasificacion()
        {
            return @"
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
            string consultaClasificacion = ObtenerConsultaClasificacion();
            var clasificacionConsulta = conn.Query<ClasificacionData>(consultaClasificacion, $"{fechaBuscada}%", companiadm);
            return clasificacionConsulta.Select(c => c.DESCRIPCION).ToList();
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

    public class ClasificacionData
    {
        public string DESCRIPCION { get; set; }
    }
}
