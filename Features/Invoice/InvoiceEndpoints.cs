using Features.Invoice;
using Microsoft.AspNetCore.Mvc;
namespace Feature.Invoice;




public static class InvoiceEndpoints
{


    
    ///invoice endpoint to get the invoice pdf file
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        // create the endpoint with required parameters for report generation
        app.MapGet("/v1/payments{id:guid}/invoice" , async(
            Guid id , 
            [AsParameters] InvoiceService invoiceService,
            InvoiceOptions invoiceOptions,
            CancellationToken ct

        ) =>
        {
            // call the service to get or generate the pdf
            var pdfBytes = await invoiceService.GetOrGenerateInvoicePdf(id , invoiceOptions , ct);

            // if the pdf bytes is null , return the error message
            if(pdfBytes is null)
            {
                Console.WriteLine("Failed to generate pdf invoice");
                return Results.NotFound(new {error = "Completed payment record not found"});
            }
            // return the pdf bytes to the client with the correct content type

            return Results.File(pdfBytes , "application/pdf" , $"payment-{id}-invoice.pdf");
        })

        .WithTags("Invoices");
        return app;
    }
}