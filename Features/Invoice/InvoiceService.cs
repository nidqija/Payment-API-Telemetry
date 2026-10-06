using Microsoft.AspNetCore.Mvc;

namespace Features.Invoice;



public record InvoiceOptions {
    public string? AccentColor = "#000000";
    bool ShowTax = true;
    string? Watermark = "Undefined";
    
}


public static class InvoiceService {
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app){

        /*
        var group = app.MapGroup("/v1/payments/{id:guid}/invoice" , async (
            Guid id , 
            [AsParameters] InvoiceOptions invoiceOptions,
            InvoiceService invoiceService,
            CancellationToken ct
        ) => {
            Console.WriteLine("Hello World");
        });
*/
        return app; 

        
    }
}


