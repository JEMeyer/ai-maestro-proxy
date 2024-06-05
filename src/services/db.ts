import { createPool, Pool } from 'mariadb';
import { createClient, RedisClientType } from 'redis';

export const pool: Pool = createPool({
  host: process.env.SQL_HOST,
  user: process.env.SQL_USER,
  password: process.env.SQL_PW,
  database: process.env.SQL_DB,
});

// 1 year... sorry not sorry
export const CACHE_EXPIRATION_TIME_SECONDS = 24 * 60 * 60 * 365;

let redisClient: RedisClientType;

export const initializeRedis = async (): Promise<void> => {
  redisClient = createClient({
    url: `redis://${process.env.REDIS_HOST}:${process.env.REDIS_PORT}`,
  });

  redisClient.on('error', (err) => console.log('Redis Client Error', err));

  await redisClient.connect();
};

export { redisClient };
