using Microsoft.AspNetCore.Mvc;
using OuterloopLabApi.Models;
using OuterloopLabApi.Services;

namespace OuterloopLabApi.Controllers;

[ApiController]
[Route("api/conversions")]
public sealed class ConversionsController : ControllerBase
{
    private readonly ICurrencyConversionService _conversionService;

    public ConversionsController(ICurrencyConversionService conversionService)
    {
        _conversionService = conversionService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConversionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ConversionResponse>> Post([FromBody] ConvertCurrencyRequest request, CancellationToken cancellationToken)
    {
        if (!request.IsValid(out var problemDetails))
        {
            return BadRequest(problemDetails);
        }

        try
        {
            var response = await _conversionService.ConvertAndCreateAuditAsync(request.Amount, request.FromCurrency, request.ToCurrency, cancellationToken);
            return Ok(response);
        }
        catch (UpstreamProviderException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "Upstream provider failure",
                Detail = ex.Message,
                Status = StatusCodes.Status502BadGateway
            });
        }
    }

    [HttpGet("{conversionId}")]
    [ProducesResponseType(typeof(ConversionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversionResponse>> Get([FromRoute] string conversionId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(conversionId, out _))
        {
            return NotFound(new ProblemDetails
            {
                Title = "Conversion not found",
                Detail = "The conversionId format is invalid.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var record = await _conversionService.GetAuditAsync(conversionId, cancellationToken);
        if (record is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Conversion not found",
                Detail = "No audit record exists for the given conversionId.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(record);
    }
}
