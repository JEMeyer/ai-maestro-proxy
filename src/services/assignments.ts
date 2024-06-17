import { CACHE_EXPIRATION_TIME_SECONDS, pool, redisClient } from './db';

interface ModelAssignment {
  name: string;
  port: number;
  ip_addr: string;
  gpu_ids: string;
  weight: number;
}

// Returns all assignmets for a given model name, ordered by the average gpu weight for that assignment - highest first
export async function getModelAssignments(
  modelName: string
): Promise<ModelAssignment[]> {
  const cacheKey = `model:${modelName}:assignments`;

  // Check if the result is already cached in Redis
  const cachedResult = await redisClient.get(cacheKey);
  if (cachedResult) {
    return JSON.parse(cachedResult);
  }

  // If not cached, execute the query
  const query = `
    SELECT
      a.name,
      a.port,
      c.ip_addr,
      GROUP_CONCAT(DISTINCT g.id) AS gpu_ids,
      AVG(g.weight) AS avg_gpu_weight
    FROM
      assignments a
      JOIN assignment_gpus ag ON a.id = ag.assignment_id
      JOIN gpus g ON ag.gpu_id = g.id
      JOIN computers c ON g.computer_id = c.id
    WHERE
      a.model_name = ?
    GROUP BY
      a.id, a.name, a.port, c.ip_addr
    ORDER BY
      avg_gpu_weight DESC;
  `;

  const rows = await pool.execute(query, [modelName]);

  // Cache the result in Redis with an expiration time
  await redisClient.set(cacheKey, JSON.stringify(rows), {
    EX: CACHE_EXPIRATION_TIME_SECONDS,
  });

  return rows;
}

export async function deleteModelAssignmentCache() {
  const scanOptions = {
    MATCH: 'model:*:assignments',
  };

  for await (const key of redisClient.scanIterator(scanOptions)) {
    await redisClient.del(key);
  }
}
