@export()
type SqlDatabase = {
  @description('Name of the SQL database.')
  name: string

  @description('Request units per second.')
  throughput: int

  @description('Containers in the database.')
  containers: Container[]

  @description('List of identities that have read and write access to the database.')
  dataContributors: string[]
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
