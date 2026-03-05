

namespace CasaRepuestos.Config
{
    public static class Config
    {
        public static string ConnectionString =>
           Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
           ?? throw new Exception("No se encontró la variable DB_CONNECTION_STRING");
    }

}
