using SQLite;

namespace DISMOGT_REPORTES 
{
    public class resmdetallereportA
    {
        private string dbPath;
        public resmdetallereportA(string databasePath)
        {
            dbPath = databasePath;
        }

        public List<ReporteDatadetalle> ObtenerDatos(string mfecha, string companiadm)
        {
            try
            {
                string path = dbPath;
                string fechaBuscada = mfecha;

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"No se encontró la base de datos en la ruta especificada: {path}");
                }

                using (SQLiteConnection conn = new SQLiteConnection(path))
                {
                    conn.CreateTable<ReporteDatadetalle>(); // Asegurar que la tabla está creada

                    var datosConsulta = RealizarConsulta(conn, fechaBuscada, companiadm);

                    return datosConsulta;
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine($"Error de SQLite: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            return new List<ReporteDatadetalle>();
        }

        private List<ReporteDatadetalle> RealizarConsulta(SQLiteConnection conn, string fechaBuscada, string companiadm)
        {
            string nuevaConsulta = @"
            SELECT
                DET.COD_ART,
                ART.DES_ART,
                CF.DESCRIPCION AS DESCRIPCION,
                SUM(DET.CNT_MAX + (DET.CNT_MIN * 0.1)) AS UNIDADES,
                'Q ' || ROUND(SUM((DET.MON_TOT - DET.MON_DSC) * 1.12), 2) AS VENTA,
                ENC.COD_CLT,
                CLIE.NOM_CLT AS NOMBRE_CLIENTE
            FROM
                ERPADMIN_ALFAC_ENC_PED ENC
            JOIN
                ERPADMIN_CLIENTE CLIE ON ENC.COD_CLT = CLIE.COD_CLT
            JOIN
                ERPADMIN_ALFAC_DET_PED DET ON ENC.NUM_PED = DET.NUM_PED
            JOIN
                ERPADMIN_ARTICULO ART ON DET.COD_ART = ART.COD_ART
            JOIN
                ERPADMIN_CLASIFICACION_FR CF ON ART.COD_FAM = CF.CLASIFICACION AND CF.COMPANIA = ?
            WHERE
                ENC.FEC_PED LIKE ? || '%'
            GROUP BY
                DET.COD_ART,
                ART.DES_ART,
                CF.DESCRIPCION,
                ENC.COD_CLT,
                CLIE.NOM_CLT
             ORDER BY
                CF.DESCRIPCION ASC
            ";

            var datosReporte = conn.Query<ReporteDatadetalle>(nuevaConsulta, companiadm, fechaBuscada);
            return datosReporte;
        }
    }

    public class ReporteDatadetalle
    {
        public string COD_ART { get; set; }
        public string DES_ART { get; set; }
        public string DESCRIPCION { get; set; }
        public string UNIDADES { get; set; }
        public string VENTA { get; set; }
        public string COD_CLT { get; set; }
        public string NOMBRE_CLIENTE { get; set; }
    }
}
