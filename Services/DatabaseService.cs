using Dapper;
using MySql.Data.MySqlClient;
using ai_maestro_proxy.Models;

namespace ai_maestro_proxy.Services
{
    public class DatabaseService(MySqlConnection dbConnection, ILogger<DatabaseService> _logger)
    {
        public async Task<IEnumerable<Assignment>> GetAssignmentsForModelAsync(string modelName)
        {
            var sql = @"
                SELECT
                    a.port,
                    c.ip_addr AS ip,
                    GROUP_CONCAT(DISTINCT g.id) AS gpuIds,
                    AVG(g.weight) AS avgGpuWeight
                FROM
                    assignments a
                    JOIN assignment_gpus ag ON a.id = ag.assignment_id
                    JOIN gpus g ON ag.gpu_id = g.id
                    JOIN computers c ON g.computer_id = c.id
                WHERE
                    a.model_name = @ModelName
                GROUP BY
                    a.id, a.port, c.ip_addr
                ORDER BY
                    avgGpuWeight DESC;";

            return await dbConnection.QueryAsync<Assignment>(sql, new { ModelName = modelName });
        }
    }
}
