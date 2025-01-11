@export()
type SqlDatabase = {
  @description('Name of the SQL database.')
  name: string

  @description('Request units per second.')
  throughput: int

  @description('Containers in the database.')
  containers: Container[]
}

@export()
type Container = {
  @description('Name of the container.')
  name: string

  @description('Partition key path.')
  partitionKeyPath: string

  @description('Default time to live.')
  defaultTtl: int
}
