package services

import (
	"ai-maestro-proxy/db"
	"ai-maestro-proxy/models"
	"context"
	"encoding/json"
	"fmt"
	"time"
)

// 1 day in seconds
const CacheExpirationTimeSeconds = 24 * 60 * 60

func GetModelAssignments(modelName string) ([]models.ModelAssignment, error) {
	cacheKey := fmt.Sprintf("model:%s:assignments", modelName)

	// Check if the result is already cached in Redis
	cachedResult, err := db.RedisClient.Get(context.Background(), cacheKey).Result()
	if err == nil {
		var assignments []models.ModelAssignment
		err = json.Unmarshal([]byte(cachedResult), &assignments)
		if err == nil {
			return assignments, nil
		}
	}

	// If not cached, execute the query
	query := `
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
	`

	rows, err := db.DB.Query(query, modelName)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	var assignments []models.ModelAssignment
	for rows.Next() {
		var assignment models.ModelAssignment
		err := rows.Scan(&assignment.Name, &assignment.Port, &assignment.IpAddr, &assignment.GpuIds, &assignment.Weight)
		if err != nil {
			return nil, err
		}
		assignments = append(assignments, assignment)
	}

	// Cache the result in Redis with an expiration time
	cachedData, err := json.Marshal(assignments)
	if err == nil {
		db.RedisClient.Set(context.Background(), cacheKey, cachedData, time.Duration(CacheExpirationTimeSeconds)*time.Second)
	}

	return assignments, nil
}

func DeleteModelAssignmentCache() error {
	iter := db.RedisClient.Scan(context.Background(), 0, "model:*:assignments", 0).Iterator()
	for iter.Next(context.Background()) {
		err := db.RedisClient.Del(context.Background(), iter.Val()).Err()
		if err != nil {
			return err
		}
	}
	if err := iter.Err(); err != nil {
		return err
	}
	return nil
}
