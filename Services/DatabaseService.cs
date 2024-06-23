using Dapper;
using MySql.Data.MySqlClient;
using ai_maestro_proxy.Models;
using Serilog;

namespace ai_maestro_proxy.Services
{
    public class DatabaseService(string connectionString)
    {
        public async Task<IEnumerable<Assignment>> GetAssignmentsAsync(string modelName)
        {
            using MySqlConnection connection = new(connectionString);
            await connection.OpenAsync();

            string query = @"
        SELECT
            a.name,
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
            a.id, a.name, a.port, c.ip_addr
        ORDER BY
            avgGpuWeight DESC;";

            Log.Information("Executing SQL query to fetch assignments for model: {ModelName}", modelName);
            IEnumerable<Assignment> assignments = await connection.QueryAsync<Assignment>(query, new { ModelName = modelName });
            Log.Information("SQL query executed successfully. Retrieved {Count} assignments for model: {ModelName}", assignments.Count(), modelName);
            return assignments;
        }
    }
}
