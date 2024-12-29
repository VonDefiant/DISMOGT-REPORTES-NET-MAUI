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
                string path = dbPath;
                string fechaBuscada = $"{mfecha:yyyy-MM-dd}";
                string clasificacionSeleccion = "F";

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"No se encontró la base de datos en la ruta especificada: {path}");
                }

                using (SQLiteConnection conn = new SQLiteConnection(path))
                {
                    conn.CreateTable<CAJASReportData>(); // Asegurar que la tabla está creada

                    var datosConsulta = RealizarConsulta(conn, fechaBuscada, companiadm, clasificacionSeleccion);
                    var descripcionesClasificacion = ObtenerDescripcionesClasificacion(conn, fechaBuscada, companiadm);

                    // Nueva consulta para obtener el total de clientes
                    var totalClientes = ObtenerTotalClientes(conn, fechaBuscada, companiadm, clasificacionSeleccion);

                    datosConsulta.ForEach(d => d.TotalClientes = totalClientes);

                    return datosConsulta;
                }
            }
            catch (FileNotFoundException ex)
            {
                throw new FileNotFoundException($"No se encontró la base de datos en la ruta especificada: {dbPath}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener datos: {ex.Message}", ex);
            }
        }

        public List<CAJASReportData> RealizarConsulta(SQLiteConnection conn, string fechaBuscada, string companiadm, string clasificacionSeleccion)
        {
            try
            {
                // Verificar si la base de datos ya está adjuntada
                bool baseDeDatosAdjuntada = conn.TableMappings.Any(mapping => mapping.MappedType.Name == "UXC");

                if (!baseDeDatosAdjuntada)
                {
                    // Adjuntar la otra base de datos solo si no ha sido adjuntada antes
                    string dbName = "/storage/emulated/0/DISMOGTREPORTES/UXCDISMOGT.db";
                    string alias = "UXCDISMOGT";
                    string attachQuery = $"ATTACH DATABASE '{dbName}' AS {alias};";

                    conn.Execute(attachQuery);
                }

                string consulta = @"
                    SELECT 
                        PROD.COD_CLS,
                        CLA_DESC.DESCRIPCION AS AGRUPACION,
                        SUM(CNT_MAX + (CNT_MIN * 0.1)) AS UNIDADES,
                        'Q ' || ROUND(SUM((MON_TOT - DET.MON_DSC) * 1.12), 2) AS VENTA,
                        COUNT(DISTINCT COD_CLT) AS NUMERO_COBERTURAS,
                        ROUND(SUM((DET.CNT_MAX + (DET.CNT_MIN * 0.1)) / UXC.FACTOR_CONVER_6), 2) AS CAJAS
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

                var datosConsulta = conn.Query<CAJASReportData>(consulta, $"{fechaBuscada}%", companiadm, clasificacionSeleccion);

                conn.Execute("DETACH DATABASE UXCDISMOGT;");

                return datosConsulta;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al realizar la consulta: {ex.Message}", ex);
            }
        }

        public int ObtenerTotalClientes(SQLiteConnection conn, string fechaBuscada, string companiadm, string clasificacionSeleccion)
        {
            string consultaTotalClientes = $@"
                SELECT 
                    COUNT(DISTINCT COD_CLT) AS {nameof(CAJASReportData.TotalClientes)}
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
            string consultaClasificacion = ObtenerConsultaClasificacion();

            var clasificacionConsulta = conn.Query<ClasificacionData>(consultaClasificacion, $"{fechaBuscada}%", companiadm);

            var opcionesClasificacion = clasificacionConsulta.Select(c => c.DESCRIPCION).ToList();

            return opcionesClasificacion;
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
}
