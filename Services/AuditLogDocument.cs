using System;
using System.IO;
using AHON_TRACK.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AHON_TRACK.Services;

public class AuditLogDocument : IDocument
{
    public AuditLogDocumentModel Model { get; }

    public AuditLogDocument(AuditLogDocumentModel model)
    {
        Model = model;
    }
    
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;
    
    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Margin(15);
                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
    }
    
    void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\.."));
                string path = Path.Combine(projectRoot, "Assets", "LoginView", "AHON-Track-Secondary-Logo.png");
                
                column.Item().Width(150).Image(path).UseOriginalImage();
                
                column.Item()
                    .Text(Model.GymName)
                    .FontSize(20)
                    .SemiBold()
                    .FontColor(Colors.Black);

                column.Item().Text(text =>
                {
                    text.Span("Address: ").SemiBold().FontColor(Colors.Blue.Medium);
                    text.Span(Model.GymAddress);
                });

                column.Item().Text(text =>
                {
                    text.Span("Phone: ").SemiBold().FontColor(Colors.Blue.Medium);
                    text.Span(Model.GymPhone);
                });

                column.Item().Text(text =>
                {
                    text.Span("Email: ").SemiBold().FontColor(Colors.Blue.Medium);
                    text.Span(Model.GymEmail);
                });
            });

            row.RelativeItem().Column(column =>
            {
                column.Item().AlignRight()
                    .Text("AUDIT LOG REPORT")
                    .FontColor(Colors.Red.Medium)
                    .FontSize(18)
                    .SemiBold();

                column.Item().AlignRight().Text(text =>
                {
                    text.Span("Generated on: ").SemiBold().FontColor(Colors.Blue.Medium);
                    text.Span($"{Model.GeneratedDate:MMMM dd, yyyy}");
                });
            });
        });
    }
    
    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(40).Column(column =>
        {
            column.Spacing(5);
            column.Item().Element(ComposeTable);
            column.Item().PaddingTop(10).AlignRight().Text(text =>
            {
                text.Span("Total Audit Logs: ").FontSize(14).SemiBold();
                text.Span($"{Model.TotalAuditLogs:N0}").FontSize(14).SemiBold().FontColor(Colors.Green.Darken2);
            });
        });
    }
    
    void ComposeTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(50);
                columns.RelativeColumn(23);
                columns.RelativeColumn(20);
                columns.RelativeColumn(40);
                columns.ConstantColumn(140);
            });

            table.Header(header =>
            {
                header.Cell().Element(CellStyle).Text("ID");
                header.Cell().Element(CellStyle).Text("Name");
                header.Cell().Element(CellStyle).Text("Position");
                header.Cell().Element(CellStyle).AlignLeft().Text("Action");
                header.Cell().Element(CellStyle).AlignRight().Text("Date & Time");

                static IContainer CellStyle(IContainer container)
                {
                    return container
                        .DefaultTextStyle(x => x.SemiBold())
                        .PaddingVertical(5)
                        .BorderBottom(1)
                        .BorderColor(Colors.Black);
                }
            });

            foreach (var item in Model.Items)
            {
                table.Cell().Element(CellStyle).Text(item.ID.ToString()).FontSize(10).SemiBold();
                table.Cell().Element(CellStyle).Text(item.Name).FontSize(10);
                table.Cell().Element(CellStyle).Text(item.Position).FontSize(10);
                table.Cell().Element(CellStyle).AlignLeft().Text(item.Action).FontSize(10);
                table.Cell().Element(CellStyle).AlignRight().Text(item.DateAndTime.ToString("MMM dd, yyyy h:mm tt")).FontSize(10);

                static IContainer CellStyle(IContainer container)
                {
                    return container
                        .BorderBottom(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .PaddingVertical(5);
                }
            }
        });
    }
}