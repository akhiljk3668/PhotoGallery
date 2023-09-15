using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using PhotoGallery.Data;
using PhotoGallery.Models;
using PhotoGallery.ViewModels;

namespace PhotoGallery.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly PhotoGalleryDbContext _photoGalleryDbContext;
    private readonly IConfiguration _configuration;

    public HomeController(ILogger<HomeController> logger, IWebHostEnvironment webHostEnvironment, PhotoGalleryDbContext photoGalleryDbContext,IConfiguration configuration)
    {
        _logger = logger;
        _webHostEnvironment = webHostEnvironment;
        _photoGalleryDbContext = photoGalleryDbContext;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        var imageDirectory = Path.Combine(_webHostEnvironment.WebRootPath, "images");
        if (!Directory.Exists(imageDirectory))
        {
            Directory.CreateDirectory(imageDirectory);

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var directoryInfo = new DirectoryInfo(imageDirectory);
                    var directorySecurity = directoryInfo.GetAccessControl();
                    directorySecurity.AddAccessRule(new FileSystemAccessRule("IIS_IUSRS", FileSystemRights.Modify, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                    directoryInfo.SetAccessControl(directorySecurity);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error setting directory permissions");
            }
        }

        var images = _photoGalleryDbContext.Photos.ToList();
        List<Photo> photos= new List<Photo>();
        foreach (Photo photo in images)
        {
            photo.FileName = $"data:{photo.Description};base64,{(await DownloadFile(photo.FileName))}";
            photos.Add(photo);
        }
        return View(photos);
    }

    [Authorize]
    public IActionResult Upload()
    {
        return View();
    }

    [HttpPost]
    [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
    [Authorize]
    public async Task<IActionResult> UploadImages()
    {
        var files = Request.Form.Files;
        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", fileName);

                _photoGalleryDbContext.Photos.Add(new Photo
                {
                    Name=file.Name,
                    Description=file.ContentType.ToString(),
                    FileName = fileName,
                    UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))
                });
                _photoGalleryDbContext.SaveChanges();
                bool uploaded = await UploadFile(file, fileName);
                //using (var fileStream = new FileStream(filePath, FileMode.Create))
                //{
                //    file.CopyTo(fileStream);
                //}
            }
        }

        return RedirectToAction("Index");
    }
    public async Task<bool> UploadFile(IFormFile files,string fileName)
    {
        if (files == null || files.Length <= 0)
            return false;
        string systemFileName = fileName;
        string blobstorageconnection = _configuration.GetValue<string>("BlobConnectionString");
        // Retrieve storage account from connection string.    
        CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(blobstorageconnection);
        // Create the blob client.    
        CloudBlobClient blobClient = cloudStorageAccount.CreateCloudBlobClient();
        // Retrieve a reference to a container.    
        CloudBlobContainer container = blobClient.GetContainerReference(_configuration.GetValue<string>("BlobContainerName"));
        // This also does not make a service call; it only creates a local object.    
        CloudBlockBlob blockBlob = container.GetBlockBlobReference(systemFileName);
        await using (var data = files.OpenReadStream())
        {
            await blockBlob.UploadFromStreamAsync(data);
        }
        return true;
    }
    public async Task<string> DownloadFile(string fileName)
    {
        string imageFile;
        await using (MemoryStream memoryStream = new MemoryStream())
        {
            string blobstorageconnection = _configuration.GetValue<string>("BlobConnectionString");
            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(blobstorageconnection);
            CloudBlobClient cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(_configuration.GetValue<string>("BlobContainerEndPoint"));
            CloudBlockBlob blockBlob = cloudBlobContainer.GetBlockBlobReference(fileName);
            await blockBlob.DownloadToStreamAsync(memoryStream);
            Byte[] bytes = memoryStream.ToArray();
            imageFile= Convert.ToBase64String(bytes);
        }
        return imageFile;
    }
    public async Task<bool> DeleteFile(string fileName)
    {
        string blobstorageconnection = _configuration.GetValue<string>("BlobConnectionString");
        CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(blobstorageconnection);
        CloudBlobClient cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
        string strContainerName = _configuration.GetValue<string>("BlobContainerName");
        CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(strContainerName);
        var blob = cloudBlobContainer.GetBlobReference(fileName);
        await blob.DeleteIfExistsAsync();
        return true;
    }
    [Authorize]
    public async Task<ActionResult> DeleteImage(int id)
    {
        var photo = await _photoGalleryDbContext.Photos
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)));
        if (photo != null)
        {
            _photoGalleryDbContext.Photos.Remove(photo);
            _photoGalleryDbContext.SaveChanges();

            DeleteFile(photo.FileName);
            //var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", photo.FileName!);
            //if (System.IO.File.Exists(filePath))
            //{
            //    System.IO.File.Delete(filePath);
            //}
        }

        return RedirectToAction("Index");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
