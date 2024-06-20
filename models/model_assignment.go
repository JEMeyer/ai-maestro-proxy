package models

type ModelAssignment struct {
	Name   string  `json:"name"`
	Port   int     `json:"port"`
	IpAddr string  `json:"ip_addr"`
	GpuIds string  `json:"gpu_ids"`
	Weight float64 `json:"weight"`
}
