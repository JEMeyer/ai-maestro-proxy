using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using MySql.Data.MySqlClient;
using ai_maestro_proxy.Models;

namespace ai_maestro_proxy.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<IEnumerable<Assignment>> GetAssignmentsAsync(string modelName)
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT
                    a.name,
                    a.port,
                    c.ip_addr AS Ip,
                    GROUP_CONCAT(DISTINCT g.id) AS GpuIds,
                    AVG(g.weight) AS AvgGpuWeight
                FROM
                    assignments a
                    JOIN assignment_gpus ag ON a.id = ag.assignment_id
                    JOIN gpus g ON ag.gpu_id = g.id
                    JOIN computers c ON g.computer_id = c.id
                WHERE
                    a.model_name = @ModelName
                GROUP BY
                    a.id, a.name, a.port, c.ip_addr
                ORDER BY
                    AvgGpuWeight DESC;";

            return await connection.QueryAsync<Assignment>(query, new { ModelName = modelName });
        }
    }
}
