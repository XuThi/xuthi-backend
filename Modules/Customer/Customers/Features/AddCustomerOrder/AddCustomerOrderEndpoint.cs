namespace Customer.Customers.Features.AddCustomerOrder;

// No public endpoint for this command as it's intended for internal module communication
// or admin manual adjustment if implemented later.
// Keeping an empty class to maintain folder structure pattern if desired, or can be omitted.

public class AddCustomerOrderEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Internal command, no routes exposed
    }
}
