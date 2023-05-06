using Amazon;
using Amazon.Lambda.Core;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.VisualBasic.FileIO;
using System.Data.SqlClient;
using System.Data;
using System;
using System.Drawing;
using System.Net.Mime;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using Amazon.Runtime.Internal.Endpoints.StandardLibrary;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace EMailNotifier;

public class Function
{
    ILambdaContext? LambdaContext { get; set; }
    private const string ConnectionString = "Data Source=cc-assignment.cpbtourlz8ng.ap-south-1.rds.amazonaws.com, 1433; Initial Catalog=S3BucketLogs; User ID=admin; Password='admin123';";

    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public void FunctionHandler(object input, ILambdaContext context)
    {
        LambdaContext = context;
        List<EmailData> emailData = GetEmailDataFromDB(DateTime.Now.ToString("yyyy-MM-dd"));

        if (emailData != null && emailData.Count > 0)
        {
            SendEmail(emailData);
        }
    }

    /// <summary>
    /// Gets the <see cref="List{EmailData}"/> object from database 
    /// </summary>
    /// <param name="entryDate">Entry data for which data to be featch</param>
    /// <returns>Returns <see cref="List{EmailData}"/> object</returns>
    private List<EmailData> GetEmailDataFromDB(string entryDate)
    {
        List<EmailData> result = new();

        try
        {
            using SqlConnection con = new(ConnectionString);
            SqlCommand cmd = new()
            {
                CommandType = CommandType.StoredProcedure,
                CommandText = "SSP_GetLogs",
                Connection = con
            };
            cmd.Parameters.AddWithValue("@EntryDate", entryDate);

            con.Open();
            SqlDataReader sdr = cmd.ExecuteReader();
            while (sdr.Read())
            {
                result.Add(new EmailData(
                    sdr["Uri"].ToString(),
                    sdr["Name"].ToString(),
                    int.Parse(sdr["Size"].ToString()),
                    sdr["Type"].ToString(),
                    sdr["EntryDateTime"].ToString()
                ));
            }
            con.Close();
        }
        catch (Exception e)
        {
            LambdaContext?.Logger.LogError($"Error in UpdateDatabase.");
            LambdaContext?.Logger.LogError(e.Message);
            LambdaContext?.Logger.LogError(e.StackTrace);
        }

        return result;
    }

    /// <summary>
    /// Compose email of all data and sends email 
    /// </summary>
    /// <param name="emailData"><see cref="List{EmailData}"/> object contains data</param>
    private void SendEmail(List<EmailData> emailData)
    {
        try
        {
            var client = new AmazonSimpleEmailServiceClient(Amazon.RegionEndpoint.APSouth1);
            var response = client.SendEmailAsync(new SendEmailRequest
            {
                Destination = new Destination
                {
                    ToAddresses = new List<string> {
                        @"vikesh.ldrp@gmail.com"
                    }
                },
                Message = new Message
                {
                    Body = new Body
                    {
                        Html = new Content
                        {
                            Charset = "UTF-8",
                            Data = GetHTMLBody(emailData)
                        },
                        Text = new Content
                        {
                            Charset = "UTF-8",
                            Data = "This is the message body in text format."
                        }
                    },
                    Subject = new Content
                    {
                        Charset = "UTF-8",
                        Data = "S3 Bucket Report"
                    }
                },
                Source = "2022MT93643@wilp.bits-pilani.ac.in",
            });
            LambdaContext?.Logger.LogError("Email send status: " + response.Result);
        }
        catch (Exception ex)
        {
            LambdaContext?.Logger.LogError("The email was not sent." + ex.Message);
            LambdaContext?.Logger.LogError("Error Stack: " + ex.StackTrace);
        }
    }

    /// <summary>
    /// Get the HTML body of the email
    /// </summary>
    /// <param name="emailData"><see cref="List{EmailData}"/> object contains data</param>
    /// <returns>Returns html string</returns>
    private string GetHTMLBody(List<EmailData> emailData)
    {
        StringBuilder emailBody = new();
        emailBody.Append("<html>");
        emailBody.Append("<head>");
        emailBody.Append("<style>");
        emailBody.Append("p {");
        emailBody.Append("color: black;");
        emailBody.Append('}');
        emailBody.Append("table {");
        emailBody.Append("border-collapse: collapse;");
        emailBody.Append("width: 100%;");
        emailBody.Append('}');
        emailBody.Append("th {");
        emailBody.Append("border: 1px solid #8EA9DB;");
        emailBody.Append("text-align: left;");
        emailBody.Append("padding: 8px;");
        emailBody.Append("background-color: #4472C4;");
        emailBody.Append("color: white;");
        emailBody.Append('}');
        emailBody.Append("td {");
        emailBody.Append("border: 1px solid #8EA9DB;");
        emailBody.Append("text-align: left;");
        emailBody.Append("padding: 8px;");
        emailBody.Append("color: black;");
        emailBody.Append('}');
        emailBody.Append("tr:nth-child(even) {");
        emailBody.Append("background-color: #D9E1F2;");
        emailBody.Append('}');
        emailBody.Append("</style>");
        emailBody.Append("</head>");
        emailBody.Append("<body>");
        emailBody.Append("<p>Hello,</p>");
        emailBody.Append($"<p>Below are the items added to your bucket on <b>{DateTime.Now.ToString("yyyy-MM-dd")}</b>.</p>");
        emailBody.Append("<table style=\"width: 800px\">");
        emailBody.Append("<tr>");
        emailBody.Append("<th>S3 Uri</th>");
        emailBody.Append("<th>Object Name</th>");
        emailBody.Append("<th>Object Size</th>");
        emailBody.Append("<th>Object type</th>");
        emailBody.Append("<th>Modified Date</th>");
        emailBody.Append("</tr>");

        foreach (EmailData data in emailData)
        {
            emailBody.Append("<tr>");
            emailBody.Append($"<td>{data.ObjectUri}</td>");
            emailBody.Append($"<td>{data.FileName}</td>");
            emailBody.Append($"<td>{data.Size.ToString("#,##0")} bytes</td>");
            emailBody.Append($"<td>{data.ContentType}</td>");
            emailBody.Append($"<td>{data.EntryDateTime}</td>");
            emailBody.Append("</tr>");
        }

        emailBody.Append("</table>");
        emailBody.Append("<p>Thanks and Regards,");
        emailBody.Append("<br>Vikeshkumar Mewada");
        emailBody.Append("</body>");
        emailBody.Append("</html>");

        return emailBody.ToString();
    }

    /// <summary>
    /// Class to hold data from database
    /// </summary>
    public class EmailData
    {
        /// <summary>
        /// Initialize the object of <see cref="EmailData"/>
        /// </summary>
        public EmailData(string objectUri, string fileName, int size, string contentType, string entryDateTime)
        {
            ObjectUri = objectUri;
            FileName = fileName;
            Size = size;
            ContentType = contentType;
            EntryDateTime = entryDateTime;
        }

        public string ObjectUri { get; set; }
        public string FileName { get; set; }
        public int Size { get; set; }
        public string ContentType { get; set; }
        public string EntryDateTime { get; set; }
    }
}
