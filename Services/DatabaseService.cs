using Dapper;
using MySql.Data.MySqlClient;
using AIMaestroProxy.Models;
using AIMaestroProxy.Extensions;
using static AIMaestroProxy.Models.PathCategories;

namespace AIMaestroProxy.Services
{
    public class DatabaseService
    {
        private readonly MySqlConnection _dbConnection;
        private readonly ILogger<DatabaseService> _logger;

        public DatabaseService(MySqlConnection dbConnection, ILogger<DatabaseService> logger)
        {
            _dbConnection = dbConnection;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves model assignments filtered by model name.
        /// </summary>
        public async Task<IEnumerable<ModelAssignment>> GetModelAssignmentsByModelAsync(string modelName)
        {
            var sql = @"
                SELECT
                    a.port,
                    a.name,
                    a.model_name AS modelName,
                    c.ip_addr AS ip,
                    GROUP_CONCAT(DISTINCT g.id) AS gpuIds
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
                    AVG(g.weight) DESC;";

            var parameters = new { ModelName = modelName };
            var modelAssignments = await _dbConnection.QueryAsync<ModelAssignment>(sql, parameters);

            _logger.LogDebug("Retrieved {Count} model assignments for model '{ModelName}' from database.", modelAssignments.Count(), modelName);
            return modelAssignments;
        }

        /// <summary>
        /// Retrieves model assignments filtered by service name.
        /// </summary>
        public async Task<IEnumerable<ModelAssignment>> GetModelAssignmentByServiceAsync(OutputType OutputType)
        {
            var sql = @"
                SELECT
                    a.name,
                    a.model_name AS modelName,
                    a.port,
                    c.ip_addr AS ip,
                    GROUP_CONCAT(DISTINCT g.id) AS gpuIds
                FROM
                    assignments a
                    JOIN assignment_gpus ag ON a.id = ag.assignment_id
                    JOIN gpus g ON g.id = ag.gpu_id
                    JOIN computers c ON g.computer_id = c.id
                WHERE
                    a.model_name IN (
                        SELECT name
                        FROM services
                        WHERE service_name = @OutputType
                    )
                GROUP BY
                    a.id,
                    a.port,
                    c.ip_addr;";

            var parameters = new { OutputType = OutputType.ToFriendlyString() };
            var modelAssignments = await _dbConnection.QueryAsync<ModelAssignment>(sql, parameters);

            _logger.LogDebug("Retrieved {Count} model assignments for service '{OutputType}' from database.", modelAssignments.Count(), OutputType);
            return modelAssignments;
        }
    }
}
