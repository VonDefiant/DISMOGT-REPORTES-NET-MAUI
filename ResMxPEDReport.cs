using System;
using System.Collections.Generic;
using System.IO;
using SQLite;

namespace DISMOGT_REPORTES
{
    public class ResMxPedidoReport
    {
        private readonly string _dbPath;

        public ResMxPedidoReport(string databasePath)
        {
            _dbPath = databasePath;
        }

        public List<ReportePedidos> ObtenerDatos(string fechaBuscada, string companiadm)
        {
            try
            {
                if (!File.Exists(_dbPath))
                {
                    throw new FileNotFoundException($"No se encontró la base de datos en la ruta especificada: {_dbPath}");
                }

                using (var conn = new SQLiteConnection(_dbPath))
                {
                    conn.CreateTable<ReportePedidos>(); // Asegurar que la tabla está creada
                    return RealizarConsulta(conn, fechaBuscada, companiadm);
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

        private List<ReportePedidos> RealizarConsulta(SQLiteConnection conn, string fechaBuscada, string companiadm)
        {
            const string consultaReporte = @"
                    SELECT 
                        PED.[NUM_PED],
                        PED.[COD_CLT],
                        CLIE.NOM_CLT,
                        CASE PED.[COD_CND] 
                            WHEN '01' THEN 'CONTADO'
                            ELSE 'CREDITO'
                        END as 'CONDICION',
                        CASE PED.[COD_PAIS]
                            WHEN 'FCF' THEN 'FACTURA'
                            WHEN 'CCF' THEN 'CREDITO FISCAL'
                        END as 'TIPO_DOC',
                        'Q' || ROUND(CAST(PED.[MON_CIV] AS REAL), 2) AS 'MONTO'
                    FROM [ERPADMIN_ALFAC_ENC_PED] PED 
                    JOIN [ERPADMIN_CLIENTE] CLIE ON PED.COD_CLT = CLIE.COD_CLT
                    WHERE FEC_PED LIKE ? || '%'
                    AND ESTADO <> 'C' AND TIP_DOC='1'
                ";

            return conn.Query<ReportePedidos>(consultaReporte, fechaBuscada);
        }
    }

    public class ReportePedidos
    {
        public string NUM_PED { get; set; }
        public string COD_CLT { get; set; }
        public string NOM_CLT { get; set; }
        public string CONDICION { get; set; }
        public string TIPO_DOC { get; set; }
        public string MONTO { get; set; }
    }
}
