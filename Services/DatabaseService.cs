using Dapper;
using MySql.Data.MySqlClient;
using AIMaestroProxy.Models;

namespace AIMaestroProxy.Services
{
    public class DatabaseService(MySqlConnection dbConnection, ILogger<DatabaseService> logger)
    {
        public async Task<IEnumerable<ModelAssignment>> GetModelAssignmentsAsync(string? modelName = null)
        {
            var sql = @"
                SELECT
                    a.port,
                    a.name,
                    c.ip_addr AS ip,
                    GROUP_CONCAT(DISTINCT g.id) AS gpuIds
                FROM
                    assignments a
                    JOIN assignment_gpus ag ON a.id = ag.assignment_id
                    JOIN gpus g ON ag.gpu_id = g.id
                    JOIN computers c ON g.computer_id = c.id";

            var parameters = new DynamicParameters();
            if (!string.IsNullOrEmpty(modelName))
            {
                sql += " WHERE a.model_name = @ModelName";
                parameters.Add("@ModelName", modelName);
            }
            sql += @"
                GROUP BY
                    a.id, a.port, c.ip_addr
                ORDER BY
                    AVG(g.weight) DESC;";

            var modelAssignments = await dbConnection.QueryAsync<ModelAssignment>(sql, parameters);

            logger.LogDebug("ModelAssignments of length {modelAssignmentsCount} returned from database", modelAssignments.Count());
            return modelAssignments;
        }

        public async Task<IEnumerable<ContainerInfo>> GetLlmContainerInfosAsync()
        {
            var sql = @"
                SELECT
                    a.model_name as modelName
                    a.port,
                    c.ip_addr AS ip
                FROM
                    assignments a
                    JOIN assignment_gpus ag ON a.id = ag.assignment_id
                    JOIN gpus g ON g.id = ag.gpu_id
                    JOIN computers c ON g.computer_id = c.id
                WHERE
                    a.model_name IN (
                        SELECT name
                        FROM llms
                    )
                GROUP BY
                    a.id,
                    a.port,
                    c.ip_addr;";


            var containerInfos = await dbConnection.QueryAsync<ContainerInfo>(sql);

            logger.LogDebug("Found {modelAssignmentsCount} containers running llms", containerInfos.Count());

            return containerInfos;
        }

        public async Task<IEnumerable<ContainerInfo>> GetDiffusionContainerInfosAsync()
        {
            var sql = @"
                SELECT
                    a.model_name as modelName
                    a.port,
                    c.ip_addr AS ip
                FROM
                    assignments a
                    JOIN assignment_gpus ag ON a.id = ag.assignment_id
                    JOIN gpus g ON g.id = ag.gpu_id
                    JOIN computers c ON g.computer_id = c.id
                WHERE
                    a.model_name IN (
                        SELECT name
                        FROM diffusors
                    )
                GROUP BY
                    a.id,
                    a.port,
                    c.ip_addr;";


            var containerInfos = await dbConnection.QueryAsync<ContainerInfo>(sql);

            logger.LogDebug("Found {modelAssignmentsCount} containers running llms", containerInfos.Count());

            return containerInfos;
        }
    }
}
