using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.UI;

public partial class Checkout : Page
{
    protected void PlaceOrderButton_Click(object sender, EventArgs e)
    {
        var cart = Session["Cart"] as Dictionary<int, int>;
        if (cart == null || cart.Count == 0)
        {
            ErrorLabel.Text = "Your cart is empty.";
            return;
        }

        string address = ShippingAddressBox.Text;
        string connectionString = ConfigurationManager.ConnectionStrings["ShopDb"].ConnectionString;

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            var insert = new SqlCommand(
                "INSERT INTO Orders (CustomerId, ShippingAddress) VALUES (" + Session["CustomerId"] + ", '" + address + "'); SELECT SCOPE_IDENTITY();",
                connection);
            int orderId = Convert.ToInt32(insert.ExecuteScalar());

            foreach (var item in cart)
            {
                var update = new SqlCommand(
                    "UPDATE Inventory SET Stock = Stock - " + item.Value + " WHERE ProductId = " + item.Key,
                    connection);
                update.ExecuteNonQuery();
            }

            Session.Remove("Cart");
            Response.Redirect("Confirmation.aspx?orderId=" + orderId);
        }
    }
}
