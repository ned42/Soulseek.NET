﻿namespace WebAPI.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using Soulseek;
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using WebAPI.DTO;
    using WebAPI.Trackers;

    /// <summary>
    ///     Transfers
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class TransfersController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="TransfersController"/> class.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="client"></param>
        /// <param name="tracker"></param>
        public TransfersController(IConfiguration configuration, ISoulseekClient client, ITransferTracker tracker)
        {
            OutputDirectory = configuration.GetValue<string>("OUTPUT_DIR");
            Client = client;
            Tracker = tracker;
        }

        private ISoulseekClient Client { get; }
        private string OutputDirectory { get; }
        private ITransferTracker Tracker { get; }

        /// <summary>
        ///     Cancels the specified download.
        /// </summary>
        /// <param name="username">The username of the download source.</param>
        /// <param name="filenameSha1">The SHA1 hash of the download filename.</param>
        /// <param name="remove">A value indicating whether the tracked download should be removed after cancellation.</param>
        /// <returns></returns>
        /// <response code="204">The download was cancelled successfully.</response>
        /// <response code="404">The specified download was not found.</response>
        [HttpDelete("downloads/{username}/{filenameSha1}")]
        [Authorize]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult CancelDownload([FromRoute, Required] string username, [FromRoute, Required]string filenameSha1, [FromQuery]bool remove = false)
        {
            return CancelTransfer(TransferDirection.Download, username, filenameSha1, remove);
        }

        /// <summary>
        ///     Cancels the specified upload.
        /// </summary>
        /// <param name="username">The username of the upload destination.</param>
        /// <param name="filenameSha1">The SHA1 hash of the upload filename.</param>
        /// <param name="remove">A value indicating whether the tracked upload should be removed after cancellation.</param>
        /// <returns></returns>
        /// <response code="204">The upload was cancelled successfully.</response>
        /// <response code="404">The specified upload was not found.</response>
        [HttpDelete("uploads/{username}/{filenameSha1}")]
        [Authorize]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult CancelUpload([FromRoute, Required] string username, [FromRoute, Required]string filenameSha1, [FromQuery]bool remove = false)
        {
            return CancelTransfer(TransferDirection.Upload, username, filenameSha1, remove);
        }

        /// <summary>
        ///     Enqueues the specified download.
        /// </summary>
        /// <param name="username">The username of the download source.</param>
        /// <param name="request">The download request.</param>
        /// <returns></returns>
        /// <response code="201">The download was successfully enqueued.</response>
        /// <response code="403">The download was rejected.</response>
        /// <response code="500">An unexpected error was encountered.</response>
        [HttpPost("downloads/{username}")]
        [Authorize]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(string), 403)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> Enqueue([FromRoute, Required]string username, [FromBody]EnqueueDownloadRequest request)
        {
            var waitUntilEnqueue = new TaskCompletionSource<bool>();
            var stream = GetLocalFileStream(request.Filename, OutputDirectory);

            var cts = new CancellationTokenSource();

            var downloadTask = Client.DownloadAsync(username, request.Filename, stream, request.Size, 0, request.Token, new TransferOptions(disposeOutputStreamOnCompletion: true, stateChanged: (e) =>
            {
                Tracker.AddOrUpdate(e, cts);

                if (e.Transfer.State == TransferStates.Queued || e.Transfer.State == TransferStates.Initializing)
                {
                    waitUntilEnqueue.TrySetResult(true);
                }
            }, progressUpdated: (e) => Tracker.AddOrUpdate(e, cts)), cts.Token);

            try
            {
                // wait until either the waitUntilEnqueue task completes because the download was successfully queued, or the
                // downloadTask throws due to an error prior to successfully queueing.
                var task = await Task.WhenAny(waitUntilEnqueue.Task, downloadTask);

                if (task == downloadTask && downloadTask.Exception is AggregateException)
                {
                    var rejected = downloadTask.Exception?.InnerExceptions.Where(e => e is TransferRejectedException) ?? Enumerable.Empty<Exception>();

                    if (rejected.Any())
                    {
                        return StatusCode(403, rejected.First().Message);
                    }

                    return StatusCode(500, downloadTask.Exception.Message);
                }

                // if it didn't throw, just return ok. the download will continue waiting in the background.
                return StatusCode(201);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex);
            }
        }

        /// <summary>
        ///     Gets all downloads.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("downloads")]
        [Authorize]
        [ProducesResponseType(200)]
        public IActionResult GetDownloads()
        {
            return Ok(Tracker.Transfers
                .WithDirection(TransferDirection.Download)
                .ToMap());
        }

        /// <summary>
        ///     Gets all downloads for the specified username.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("downloads/{username}")]
        [Authorize]
        [ProducesResponseType(200)]
        public IActionResult GetDownloads([FromRoute, Required]string username)
        {
            return Ok(Tracker.Transfers
                .WithDirection(TransferDirection.Download)
                .FromUser(username)
                .ToMap());
        }

        /// <summary>
        ///     Gets the downlaod for the specified username matching the specified filename, and requests 
        ///     the current place in the remote queue of the specified download.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="filenameSha1"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">The specified download was not found.</response>
        [HttpGet("downloads/{username}/{filenameSha1}")]
        [Authorize]
        [ProducesResponseType(typeof(DTO.Transfer), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetPlaceInQueue([FromRoute, Required]string username, [FromRoute, Required]string filenameSha1)
        {
            if (Tracker.Transfers.TryGetValue(TransferDirection.Download, out var transfers)) 
            {
                if (transfers.TryGetValue(username, out var userTransfers))
                {
                    if (userTransfers.TryGetValue(filenameSha1, out var record))
                    {
                        var placeInQueue = await Client.GetDownloadPlaceInQueueAsync(username, record.Transfer.Filename);

                        record.Transfer.PlaceInQueue = placeInQueue;

                        return Ok(record.Transfer);
                    }
                }
            }

            return NotFound();
        }

        /// <summary>
        ///     Gets all uploads.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("uploads")]
        [Authorize]
        [ProducesResponseType(200)]
        public IActionResult GetUploads()
        {
            return Ok(Tracker.Transfers
                .WithDirection(TransferDirection.Upload)
                .ToMap());
        }

        /// <summary>
        ///     Gets all uploads for the specified username.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("uploads/{username}")]
        [Authorize]
        [ProducesResponseType(200)]
        public IActionResult GetUploads([FromRoute, Required]string username)
        {
            return Ok(Tracker.Transfers
                .WithDirection(TransferDirection.Upload)
                .FromUser(username)
                .ToMap());
        }

        /// <summary>
        ///     Gets the upload for the specified username matching the specified filename.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="filenameSha1"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("uploads/{username}/{filenameSha1}")]
        [Authorize]
        [ProducesResponseType(200)]
        public IActionResult GetUploads([FromRoute, Required]string username, [FromRoute, Required]string filenameSha1)
        {
            return Ok(Tracker.Transfers
                .WithDirection(TransferDirection.Upload)
                .FromUser(username)
                .WithId(filenameSha1).Transfer);
        }

        private static FileStream GetLocalFileStream(string remoteFilename, string saveDirectory)
       {
            var localFilename = remoteFilename.ToLocalOSPath();
            var path = $"{saveDirectory}{Path.DirectorySeparatorChar}{Path.GetDirectoryName(localFilename).Replace(Path.GetDirectoryName(Path.GetDirectoryName(localFilename)), "")}";

            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }

            localFilename = Path.Combine(path, Path.GetFileName(localFilename));

            return new FileStream(localFilename, FileMode.Create);
        }

        private IActionResult CancelTransfer(TransferDirection direction, string username, string filenameSha1, bool remove = false)
        {
            if (Tracker.TryGet(direction, username, filenameSha1, out var transfer))
            {
                transfer.CancellationTokenSource.Cancel();

                if (remove)
                {
                    Tracker.TryRemove(direction, username, filenameSha1);
                }

                return NoContent();
            }

            return NotFound();
        }
    }
}