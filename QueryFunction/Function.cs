using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace QueryFunction
{
    public class Function
    {
        private readonly Table _dataDynamoDbTable;

        public Function()
        {
            var _dynamoDbClient = new AmazonDynamoDBClient();

            string tableName = Environment.GetEnvironmentVariable("TableName");
            _dataDynamoDbTable = Table.LoadTable(_dynamoDbClient, tableName);
        }

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                ExtractParameters(request, out string userId, out string dataId);

                var values = await QueryTable(userId, dataId);

                string body = JsonConvert.SerializeObject(values);

                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = body
                };
            }
            catch (Exception ex)
            {
                context.Logger.LogLine(JsonConvert.SerializeObject(ex, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                }));
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int) HttpStatusCode.BadRequest,
                    Body = $"{ex.GetType().Name} - {ex.Message}"
                };
            }
        }

        private async Task<List<string>> QueryTable(string userId, string dataId)
        {
            var config = new QueryOperationConfig();

            if (string.IsNullOrWhiteSpace(dataId))
            {
                config.KeyExpression = new Expression
                {
                    ExpressionStatement = $"UserId = :{nameof(userId)}",
                    ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                    {
                        { $":{nameof(userId)}", userId }
                    }
                };
            }
            else
            {
                config.KeyExpression = new Expression
                {
                    ExpressionStatement = $"UserId = :{nameof(userId)} and DataId = :{nameof(dataId)}",
                    ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                    {
                        { $":{nameof(userId)}", userId },
                        { $":{nameof(dataId)}", dataId }
                    }
                };
            }

            var dataDocuments = await _dataDynamoDbTable.Query(config).GetRemainingAsync();

            var values = dataDocuments.Select(dataDocument => dataDocument["DataAsJson"]?.AsString()).ToList();
            return values;
        }

        private void ExtractParameters(APIGatewayProxyRequest request, out string userId, out string dataId)
        {
            userId = request.PathParameters.ContainsKey(nameof(userId))? request.PathParameters[nameof(userId)] : null;
            dataId = request.PathParameters.ContainsKey(nameof(dataId))? request.PathParameters[nameof(dataId)] : null;
        }
    }
}
