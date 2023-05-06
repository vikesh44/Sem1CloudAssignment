using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using HeyRed.Mime;
using ImageMagick;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace BucketTracker;

public class Function
{
    IAmazonS3 S3Client { get; set; }
    ILambdaContext lambdaContext { get; set; }

    private const string BucketUriFormat = "s3://{0}/{1}";
    private const string ConnectionString = "Data Source=cc-assignment.cpbtourlz8ng.ap-south-1.rds.amazonaws.com, 1433; Initial Catalog=S3BucketLogs; User ID=admin; Password='admin123';";
    private readonly List<string> ImageTypes = new()
    {
        "image/jpeg",
        "image/bmp",
        "image/png",
        "image/gif"
    };

    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {
        S3Client = new AmazonS3Client();
    }

    /// <summary>
    /// Constructs an instance with a preconfigured S3 client. This can be used for testing the outside of the Lambda environment.
    /// </summary>
    /// <param name="s3Client"></param>
    public Function(IAmazonS3 s3Client)
    {
        this.S3Client = s3Client;
    }

    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
    /// to respond to S3 notifications.
    /// </summary>
    /// <param name="evnt"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task FunctionHandler(S3Event evnt, ILambdaContext context)
    {
        lambdaContext = context;
        var eventRecords = evnt.Records ?? new List<S3Event.S3EventNotificationRecord>();
        string fileName;
        foreach (var record in eventRecords)
        {
            var s3Event = record.S3;
            if (s3Event == null)
            {
                continue;
            }

            fileName = s3Event.Object.Key.Replace('+', ' ');

            try
            {
                var objectMetaData = await this.S3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, fileName);

                var objectResponse = await S3Client.GetObjectAsync(s3Event.Bucket.Name, fileName);

                UpdateDatabase(string.Format("s3://{0}/{1}", s3Event.Bucket.Name, fileName),
                               fileName,
                               s3Event.Object.Size,
                               MimeTypesMap.GetExtension(objectResponse.Headers.ContentType),
                               objectResponse.LastModified.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                if (ImageTypes.Contains(objectMetaData.Headers.ContentType))
                {
                    lambdaContext.Logger.LogError("Image file detected");
                    await CreateThumbnail(objectResponse.ResponseStream, s3Event.Bucket.Name + "/Thumbnails", fileName.Substring(6));
                }
            }
            catch (Exception ex)
            {
                lambdaContext.Logger.LogError($"Error with object {fileName} from bucket {s3Event.Bucket.Name}.");
                lambdaContext.Logger.LogError(ex.Message);
                lambdaContext.Logger.LogError(ex.StackTrace);
                throw;
            }
        }
    }

    private void UpdateDatabase(string uri, string fileName, long size, string fileType, string lastModified)
    {
        try
        {
            using SqlConnection con = new(ConnectionString);
            SqlCommand cmd = new()
            {
                CommandType = CommandType.StoredProcedure,
                CommandText = "SSP_AddLog",
                Connection = con
            };

            cmd.Parameters.AddWithValue("@Uri", uri);
            cmd.Parameters.AddWithValue("@Name", fileName);
            cmd.Parameters.AddWithValue("@Size", size);
            cmd.Parameters.AddWithValue("@Type", fileType);
            cmd.Parameters.AddWithValue("@EntryDateTime", lastModified);
            cmd.Parameters.AddWithValue("@IsEmailed", 0);

            con.Open();
            cmd.ExecuteNonQuery();
            con.Close();
        }
        catch (Exception e)
        {
            lambdaContext.Logger.LogError($"Error in UpdateDatabase.");
            lambdaContext.Logger.LogError(e.Message);
            lambdaContext.Logger.LogError(e.StackTrace);
        }
    }

    private async Task CreateThumbnail(Stream responseStream, string bucketName, string fileName)
    {

        Stream thumbnailImageStream = new MemoryStream();

        try
        {
            using (MagickImageCollection collection = new MagickImageCollection(responseStream))
            {
                foreach (MagickImage image in collection)
                {
                    image.Resize(100, 100);
                }

                thumbnailImageStream = new MemoryStream();
                collection.Write(thumbnailImageStream);
                thumbnailImageStream.Position = 0;
            }
        }
        catch (Exception e)
        {
            lambdaContext.Logger.LogLine("Error in CreateThumbnail: " + e.Message);
        }

        await PutS3Object(bucketName, fileName, thumbnailImageStream);
    }

    public async Task<bool> PutS3Object(string bucket, string key, Stream content)
    {
        try
        {
            using (var client = new AmazonS3Client(Amazon.RegionEndpoint.APSouth1))
            {
                var request = new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = key,
                    InputStream = content
                };
                var response = await client.PutObjectAsync(request);
            }
            return true;
        }
        catch (Exception ex)
        {
            lambdaContext.Logger.LogError($"Error in PutS3Object." + ex.Message);
            return false;
        }
    }
}