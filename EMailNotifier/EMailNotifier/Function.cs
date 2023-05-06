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
    ILambdaContext lambdaContext { get; set; }
    private const string ConnectionString = "Data Source=cc-assignment.cpbtourlz8ng.ap-south-1.rds.amazonaws.com, 1433; Initial Catalog=S3BucketLogs; User ID=admin; Password='admin123';";

    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task FunctionHandler(object input, ILambdaContext context)
    {
        lambdaContext = context;
        List<EmailData> emailData = GetEntries(DateTime.Now.ToString("yyyy-MM-dd"));

        if (emailData != null && emailData.Count > 0)
        {
            SendEmail(emailData);
        }
    }

    private List<EmailData> GetEntries(string entryDate)
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
                result.Add(new EmailData
                {
                    ObjectUri = sdr["Uri"].ToString(),
                    FileName = sdr["Name"].ToString(),
                    Size = int.Parse(sdr["Size"].ToString()),
                    ContentType = sdr["Type"].ToString(),
                    EntryDateTime = sdr["EntryDateTime"].ToString()
                });
            }
            con.Close();
        }
        catch (Exception e)
        {
            lambdaContext.Logger.LogError($"Error in UpdateDatabase.");
            lambdaContext.Logger.LogError(e.Message);
            lambdaContext.Logger.LogError(e.StackTrace);
        }

        return result;
    }

    private async Task SendEmail(List<EmailData> emailData)
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
            lambdaContext.Logger.LogError("Email send status: " + response.Result);
        }
        catch (Exception ex)
        {
            lambdaContext.Logger.LogError("The email was not sent." + ex.Message);
            lambdaContext.Logger.LogError("Error Stack: " + ex.StackTrace);
        }
    }

    private string GetHTMLBody(List<EmailData> emailData)
    {
        StringBuilder emailBody = new StringBuilder();
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

    public class EmailData
    {
        public string ObjectUri { get; set; }
        public string FileName { get; set; }
        public int Size { get; set; }
        public string ContentType { get; set; }
        public string EntryDateTime { get; set; }
    }
}
