using System;
using System.Collections.Generic;
using System.IO;
using SQLite;

namespace DISMOGT_REPORTES 
{
    public class ResMxClient
    {
        private string dbPath;

        public ResMxClient(string databasePath)
        {
            dbPath = databasePath;
        }

        public List<PedidoReportData> ObtenerDatos(string mfecha, string companiadm)
        {
            try
            {
                string path = dbPath;
                string fechaBuscada = mfecha;
                string clientebuscado = "F";

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"No se encontró la base de datos en la ruta especificada: {path}");
                }

                using (SQLiteConnection conn = new SQLiteConnection(path))
                {
                    conn.CreateTable<PedidoReportData>(); // Asegurar que la tabla está creada

                    var datosConsulta = RealizarConsulta(conn, fechaBuscada, clientebuscado);
                    return datosConsulta;
                }
            }
            catch (Exception e)
            {
                // Manejo de errores
                Console.WriteLine($"Error: {e.Message}");
                return new List<PedidoReportData>();
            }
        }

        public List<PedidoReportData> RealizarConsulta(SQLiteConnection conn, string fechaBuscada, string clientebuscado)
        {
            string consulta = @"
                SELECT 
                    ENC.NUM_PED AS NUM_PED,
                    DET.COD_ART AS ARTICULO,
                    ART.DES_ART AS DESCRIPCION,
                    (DET.CNT_MAX + (DET.CNT_MIN * 0.1)) AS UNIDADES,
                    'Q ' || ROUND(SUM((DET.MON_TOT - DET.MON_DSC) * 1.12), 2) AS VENTA,
                    ENC.COD_CLT,
                    CLIE.NOM_CLT AS NOMBRE 
                FROM 
                    ERPADMIN_ALFAC_ENC_PED ENC 
                JOIN 
                    ERPADMIN_CLIENTE CLIE ON ENC.COD_CLT = CLIE.COD_CLT 
                JOIN 
                    ERPADMIN_ALFAC_DET_PED DET ON ENC.NUM_PED = DET.NUM_PED 
                JOIN 
                    ERPADMIN_ARTICULO ART ON DET.COD_ART = ART.COD_ART 
                WHERE
                    ENC.COD_CLT LIKE ? ||  '%'
                    AND ESTADO <> 'C'
                    AND FEC_PED LIKE ? || '%'
                GROUP BY 
                    ENC.NUM_PED, FEC_PED, DET.COD_ART, ART.DES_ART, UNIDADES, ENC.COD_CLT, CLIE.NOM_CLT
                ORDER BY 
                    FEC_PED DESC, ENC.NUM_PED DESC, ARTICULO;
            ";

            var datosConsulta = conn.Query<PedidoReportData>(consulta, clientebuscado, fechaBuscada);
            return datosConsulta;
        }
    }

    public class PedidoReportData
    {
        public int NUM_PED { get; set; }
        public string ARTICULO { get; set; }
        public string DESCRIPCION { get; set; }
        public int UNIDADES { get; set; }
        public string VENTA { get; set; }
        public string COD_CLT { get; set; }
        public string NOMBRE { get; set; }
    }
}
