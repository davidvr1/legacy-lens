using System;
using System.Data.SqlClient;
using System.Web.UI;

public partial class OrderLookup : Page
{
    private const string ConnectionString =
        "Server=db01;Database=Shop;User Id=web;Password=Fake_Web_Pass1;";

    protected void Page_Load(object sender, EventArgs e)
    {
        var orderId = Request.QueryString["orderId"];
        Response.Write("Looking up order " + Request.QueryString["orderId"]);
        Response.Write(Server.HtmlEncode(orderId));

        using var conn = new SqlConnection(ConnectionString);
        var unsafeCmd = new SqlCommand("SELECT * FROM Orders WHERE Id = " + orderId, conn);
        // var old = new SqlCommand("SELECT * FROM Orders WHERE Id = " + orderId, conn);

        var safeCmd = new SqlCommand("SELECT * FROM Orders WHERE Id = @id", conn);
        safeCmd.Parameters.AddWithValue("@id", orderId);
    }

    protected void Search_Click(object sender, EventArgs e)
    {
        var sql = "SELECT * FROM Customers WHERE Name LIKE '%" + NameBox.Text + "%'";
        var cmd = new SqlCommand(sql);
    }
}
