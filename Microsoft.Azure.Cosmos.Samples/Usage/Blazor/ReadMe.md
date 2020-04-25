#Blazor sample
This contains a sample of a Blazor application using Cosmos DB.

The application reads all the items from a container, and allows the user to create new items in the container with a button click.

## Setup

1. Update the "Microsoft.Azure.Cosmos.Samples\Usage\Blazor\appsetting.json" with the correct connection string.
2. Create a database with the name "BlazorDB"
3. Create a container with the name "BlazorContainer" with partition key "/id"
4. Run the application
