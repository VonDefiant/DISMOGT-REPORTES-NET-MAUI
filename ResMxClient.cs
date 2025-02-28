using System;
using System.Collections.Generic;
using System.IO;
using SQLite;

namespace DISMOGT_REPORTES
{
    public class ResMxClient
    {
        private readonly string dbPath;

        public ResMxClient(string databasePath)
        {
            dbPath = databasePath;
        }

        public List<PedidoReportData> ObtenerDatos(string mfecha, string clientebuscado)
        {
            try
            {
                if (!File.Exists(dbPath))
                {
                    throw new FileNotFoundException($"No se encontró la base de datos en la ruta especificada: {dbPath}");
                }

                using (SQLiteConnection conn = new SQLiteConnection(dbPath))
                {
                    conn.CreateTable<PedidoReportData>();

                    Console.WriteLine($" Ejecutando consulta con Cliente: {clientebuscado} y Fecha: {mfecha}");

                    return RealizarConsulta(conn, mfecha, clientebuscado);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($" Error en ObtenerDatos: {e.Message}");
                return new List<PedidoReportData>();
            }
        }

        public List<PedidoReportData> RealizarConsulta(SQLiteConnection conn, string fechaBuscada, string clientebuscado)
        {
            try
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
                        ENC.COD_CLT = ? 
                        AND ESTADO <> 'C'
                        AND FEC_PED LIKE ? || '%'
                    GROUP BY 
                        ENC.NUM_PED, FEC_PED, DET.COD_ART, ART.DES_ART, UNIDADES, ENC.COD_CLT, CLIE.NOM_CLT
                    ORDER BY 
                        FEC_PED DESC, ENC.NUM_PED DESC, ARTICULO;
                ";

                Console.WriteLine($" Parámetros enviados - Cliente: {clientebuscado}, Fecha: {fechaBuscada}");

                var datosConsulta = conn.Query<PedidoReportData>(consulta, clientebuscado, fechaBuscada);

                Console.WriteLine($" Registros devueltos por la consulta: {datosConsulta.Count}");

                return datosConsulta;
            }
            catch (Exception e)
            {
                Console.WriteLine($" Error en RealizarConsulta: {e.Message}");
                return new List<PedidoReportData>();
            }
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
