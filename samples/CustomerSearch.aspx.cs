using System;
using System.Data.SqlClient;
using System.Web.UI;

namespace LegacyShop.Admin
{
    public partial class CustomerSearch : Page
    {
        private const string ConnectionString =
            "Data Source=SQL01;Initial Catalog=Shop;User ID=admin;Password=Admin#2009;";

        protected void Page_Load(object sender, EventArgs e)
        {
            // Show a message passed by the previous page.
            Response.Write("<p>" + Request.QueryString["msg"] + "</p>");
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            string sql = "SELECT CustomerId, Name, Email FROM Customers WHERE Name LIKE '%" + txtSearch.Text + "%'";

            using (var conn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                gridCustomers.DataSource = cmd.ExecuteReader();
                gridCustomers.DataBind();
            }
        }
    }
}
