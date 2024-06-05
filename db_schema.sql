CREATE TABLE `ai-maestro-db`.computers (
	id INT auto_increment NOT NULL,
	name varchar(100) NOT NULL,
	ip_addr varchar(15) NOT NULL,
	CONSTRAINT computers_pk PRIMARY KEY (id)
)
ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4
COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `ai-maestro-db`.gpus (
	id INT auto_increment NOT NULL,
	name varchar(100) NOT NULL,
	vram_size FLOAT NOT NULL,
	computer_id INT NOT NULL,
	weight FLOAT NULL,
	CONSTRAINT gpus_pk PRIMARY KEY (id),
	CONSTRAINT gpus_computers_FK FOREIGN KEY (computer_id) REFERENCES `ai-maestro-db`.computers(id)
)
ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4
COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `ai-maestro-db`.llms (
	name varchar(100) NOT NULL,
	`size` DOUBLE NOT NULL,
	CONSTRAINT llms_pk PRIMARY KEY (name)
)
ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4
COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `ai-maestro-db`.diffusors (
	name varchar(100) NOT NULL,
	`size` DOUBLE NOT NULL,
	CONSTRAINT llms_pk PRIMARY KEY (name)
)
ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4
COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `ai-maestro-db`.assignments (
	id INT auto_increment NOT NULL,
	name varchar(100) NOT NULL,
	model_name varchar(100) NOT NULL,
	port INT NOT NULL,
	CONSTRAINT assignments_pk PRIMARY KEY (id)
)
ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4
COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `ai-maestro-db`.assignment_gpus (
	assignment_id INT NOT NULL,
	gpu_id INT NOT NULL,
	CONSTRAINT assignment_gpus_pk PRIMARY KEY (assignment_id,gpu_id),
	CONSTRAINT assignment_gpus_assignments_FK FOREIGN KEY (assignment_id) REFERENCES `ai-maestro-db`.assignments(id),
	CONSTRAINT assignment_gpus_gpus_FK FOREIGN KEY (gpu_id) REFERENCES `ai-maestro-db`.gpus(id)
)
ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4
COLLATE=utf8mb4_0900_ai_ci;
