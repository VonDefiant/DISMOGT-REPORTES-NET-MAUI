using System;
using System.Collections.Generic;
using System.IO;
using SQLite;

namespace DISMOGT_REPORTES
{
    public class ResMxFamReport
    {
        private readonly string _dbPath;

        public int CuentaClientes { get; private set; }

        public ResMxFamReport(string databasePath)
        {
            _dbPath = databasePath;
        }

        public List<ReporteData> ObtenerDatos(string mfecha, string companiadm)
        {
            try
            {
                if (!File.Exists(_dbPath))
                {
                    throw new FileNotFoundException($"No se encontró la base de datos en la ruta especificada: {_dbPath}");
                }

                using (var conn = new SQLiteConnection(_dbPath))
                {
                    // Cuenta los clientes con venta según la base de datos
                    var clientes = conn.Query<VisitaDocumento>("SELECT CLIENTE FROM ERPADMIN_VISITA_DOCUMENTO WHERE INICIO LIKE ?", mfecha + "%");
                    CuentaClientes = clientes.Count;

                    conn.CreateTable<ReporteData>(); // Asegurar que la tabla está creada

                    var datosConsulta = RealizarConsulta(conn, mfecha, companiadm);

                    return datosConsulta;
                }
            }
            catch (Exception e)
            {
                // Manejo de errores
                Console.WriteLine($"Error: {e.Message}");
                return new List<ReporteData>();
            }
        }

        private List<ReporteData> RealizarConsulta(SQLiteConnection conn, string fechaBuscada, string companiadm)
        {
            const string consultaReporte = @"
            SELECT 
                COD_FAM,
                CLA.DESCRIPCION,
                SUM(CNT_MAX + (CNT_MIN * 0.1)) AS UNIDADES,
                'Q ' || ROUND(SUM((MON_TOT - DET.MON_DSC) * 1.12), 2) AS VENTA,
                COUNT(DISTINCT COD_CLT) AS NUMERO_CLIENTES
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
                COD_FAM, CLA.DESCRIPCION
            HAVING
                SUM((MON_TOT - DET.MON_DSC) * 1.12) > 0
        ";

            const string consultaClientes = @"
            SELECT COUNT(DISTINCT CLIENTE) AS TotalClientes
            FROM ERPADMIN_VISITA_DOCUMENTO
            WHERE INICIO LIKE ? || '%'
        ";

            // Ejecutar consultas
            var datosReporte = conn.Query<ReporteData>(consultaReporte, fechaBuscada, companiadm);
            var totalClientes = conn.ExecuteScalar<int>(consultaClientes, fechaBuscada);

            // Agregar el resultado de la consulta de clientes al primer resultado
            if (datosReporte.Count > 0)
            {
                datosReporte[0].TotalClientes = totalClientes;
            }

            return datosReporte;
        }
    }

    public class ReporteData
    {
        public string COD_FAM { get; set; }
        public string DESCRIPCION { get; set; }
        public double UNIDADES { get; set; }
        public string VENTA { get; set; }
        public int NUMERO_CLIENTES { get; set; }
        public int TotalClientes { get; set; }
    }

    public class VisitaDocumento
    {
        public string CLIENTE { get; set; }
    }
}
