namespace TaskFlow.Web.Models;

public class StatusViewModel
{
    public int Code { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public static StatusViewModel For(int code)
    {
        return code switch
        {
            403 => new StatusViewModel
            {
                Code = 403,
                Title = "Access denied",
                Message = "You don't have permission to do that. " +
                          "If you think you should, ask a project manager or workspace admin."
            },
            404 => new StatusViewModel
            {
                Code = 404,
                Title = "Page not found",
                Message = "The page you're looking for doesn't exist, " +
                          "or you don't have access to it."
            },
            _ => new StatusViewModel
            {
                Code = code is >= 400 and < 600 ? code : 500,
                Title = "Something went wrong",
                Message = "The request couldn't be completed. Please try again."
            }
        };
    }
}
