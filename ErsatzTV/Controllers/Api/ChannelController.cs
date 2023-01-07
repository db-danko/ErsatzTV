﻿using System.ComponentModel.DataAnnotations;
using ErsatzTV.Application.Channels;
using ErsatzTV.Core;
using ErsatzTV.Core.Api.Channels;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ErsatzTV.Controllers.Api;

[ApiController]
public class ChannelController
{
    private readonly IMediator _mediator;

    public ChannelController(IMediator mediator) => _mediator = mediator;

    [HttpGet("/api/channels")]
    public async Task<List<ChannelResponseModel>> GetAll() =>
        await _mediator.Send(new GetAllChannelsForApi());

    [HttpGet("/api/channels/{id:int}")]
    public async Task<Option<ChannelResponseModel>> GetOne(int id) =>
        await _mediator.Send(new GetChannelByIdForApi(id));

    [HttpPut("/api/channels/update")]
    public async Task<Either<BaseError, UpdateChannelResult>> UpdateOneBlock(
        [Required] [FromBody]
        UpdateChannel request) => await _mediator.Send(request);
}
