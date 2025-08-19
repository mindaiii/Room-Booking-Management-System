﻿using BookingManagement.Repositories.Models;
using BookingManagement.Services.DTOs;
using BookingManagement.Services.Interfaces;
using BookingManagement.Services.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using BookingManagement.Admin.MVC.Hubs;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace BookingManagement.Admin.MVC.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    public class BookingController : Controller
    {
        private readonly IBookingService _bookingService;
        private readonly IRoomService _roomService;
        private readonly IUserService _userService;
        private readonly ITimeSlotService _timeSlotService;
        private readonly INotificationService _notificationService;
        private readonly IHubContext<BookingHub> _hubContext;
        private readonly ISignalRService _signalRService;
        private static DateTime _lastCheckTime = DateTime.Now;

        public BookingController(
            IBookingService bookingService,
            IRoomService roomService,
            IUserService userService,
            ITimeSlotService timeSlotService,
            INotificationService notificationService,
            IHubContext<BookingHub> hubContext,
            ISignalRService signalRService)
        {
            _bookingService = bookingService;
            _roomService = roomService;
            _userService = userService;
            _timeSlotService = timeSlotService;
            _notificationService = notificationService;
            _hubContext = hubContext;
            _signalRService = signalRService;
        }

        // GET: Booking
        public async Task<IActionResult> Index(string status = "all", string searchString = "", string dateFilter = "")
        {
            var bookings = await _bookingService.GetAllAsync();

            // Lọc theo trạng thái nếu có yêu cầu
            if (status != "all")
            {
                if (int.TryParse(status, out int statusInt))
                {
                    bookings = bookings.Where(b => b.Status == statusInt);
                }
            }

            // Lọc theo từ khóa tìm kiếm (phòng hoặc người dùng)
            if (!string.IsNullOrEmpty(searchString))
            {
                bookings = bookings.Where(b => 
                    b.Room.RoomName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                    b.User.FullName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                    b.User.Email.Contains(searchString, StringComparison.OrdinalIgnoreCase));
            }

            // Lọc theo ngày
            if (!string.IsNullOrEmpty(dateFilter) && DateOnly.TryParse(dateFilter, out DateOnly filterDate))
            {
                bookings = bookings.Where(b => b.BookingDate == filterDate);
            }

            // Sắp xếp: mới nhất lên đầu, sau đó là status (chờ duyệt lên đầu)
            bookings = bookings.OrderByDescending(b => b.CreatedAt)
                               .ThenBy(b => b.Status);

            // Log số lượng booking có status = 2 (Đã duyệt)
            var approvedBookingCount = bookings.Count(b => b.Status == 2);
            Console.WriteLine($"Số lượng booking đã duyệt (status = 2): {approvedBookingCount}");

            // Lưu các giá trị filter cho view
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentDate = dateFilter;
            ViewBag.TodayDate = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
            
            // Kiểm tra có booking mới nào từ lần cuối truy cập trang không
            var hasNewBookings = bookings.Any(b => b.Status == 1 && b.CreatedAt > _lastCheckTime);
            if (hasNewBookings)
            {
                ViewBag.HasNewBookings = true;
                ViewBag.NewBookingsCount = bookings.Count(b => b.Status == 1 && b.CreatedAt > _lastCheckTime);
            }

            // Cập nhật thời gian kiểm tra cuối cùng
            _lastCheckTime = DateTime.Now;

            return View(bookings);
        }

        // API: Booking/CheckNewBookings - Kiểm tra có booking mới không
        [HttpGet]
        public async Task<JsonResult> CheckNewBookings()
        {
            var bookings = await _bookingService.GetAllAsync();
            var hasNewBookings = bookings.Any(b => b.CreatedAt > _lastCheckTime && b.Status == 1); // 1: Chờ duyệt
            
            if (hasNewBookings)
            {
                _lastCheckTime = DateTime.Now;
            }
            
            return Json(hasNewBookings);
        }

        // API: Booking/TestNotification - Test sending notification to a user
        [HttpGet]
        public async Task<IActionResult> TestNotification(int userId)
        {
            // Send a test notification to the user
            try
            {
                var message = $"This is a test notification for user {userId} at {DateTime.Now.ToString("HH:mm:ss")}";
                
                // Send test notification using multiple methods
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", message);
                await _hubContext.Clients.Group(userId.ToString()).SendAsync("ReceiveNotification", message);
                await _hubContext.Clients.Group(userId.ToString()).SendAsync("ReceiveBookingApproval", message, 123);
                
                Console.WriteLine($"Test notification sent to user {userId}: {message}");
                
                return Json(new { success = true, message = $"Test notification sent to user {userId}" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending test notification: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: Booking/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var booking = await _bookingService.GetByIdAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }

        // GET: Booking/Approve/5
        public async Task<IActionResult> Approve(int id)
        {
            var booking = await _bookingService.GetByIdAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            // Chỉ cho phép duyệt booking đang ở trạng thái chờ duyệt (1)
            if (booking.Status != 1)
            {
                TempData["ErrorMessage"] = "Chỉ có thể phê duyệt yêu cầu đang ở trạng thái chờ duyệt.";
                return RedirectToAction(nameof(Index));
            }

            return View(booking);
        }

        // POST: Booking/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveConfirmed(int id)
        {
            var booking = await _bookingService.GetByIdAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            if (booking.Status != 1)
            {
                TempData["ErrorMessage"] = "Chỉ có thể phê duyệt yêu cầu đang ở trạng thái chờ duyệt.";
                return RedirectToAction(nameof(Index));
            }

            // Cập nhật trạng thái thành "Đã duyệt" (2)
            booking.Status = 2;
            booking.UpdatedAt = DateTime.Now;
            await _bookingService.UpdateAsync(booking);

            // Lấy thông tin phòng và khung giờ cho thông báo
            var room = await _roomService.GetRoomByIdAsync(booking.RoomId);
            var timeSlotDto = await _timeSlotService.GetTimeSlotByIdAsync(booking.TimeSlotId);

            // Tạo thông báo cho người dùng
            var notification = new Notification
            {
                UserId = booking.UserId,
                Title = "Đặt phòng đã được phê duyệt",
                Message = $"Yêu cầu đặt phòng {room.RoomName} vào ngày {booking.BookingDate.ToString("dd/MM/yyyy")}, khung giờ {timeSlotDto.StartTime}-{timeSlotDto.EndTime} đã được phê duyệt.",
                IsRead = false,
                BookingId = booking.BookingId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            
            await _notificationService.CreateAsync(notification);
            
            // Gửi thông báo real-time cho user - thử cả hai cách
            try {
                // Cách 1: Sử dụng service
                await _signalRService.SendBookingStatusUpdateAsync(booking, notification.Message);
                
                // Cách 2: Gọi trực tiếp tới hub
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", notification.Message);
                await _hubContext.Clients.Group(booking.UserId.ToString())
                    .SendAsync("ReceiveBookingApproval", notification.Message, booking.BookingId);
                
                Console.WriteLine($"Đã gửi thông báo phê duyệt tới user {booking.UserId}: {notification.Message}");
            } catch (Exception ex) {
                Console.WriteLine($"Lỗi khi gửi thông báo: {ex.Message}");
            }

            TempData["SuccessMessage"] = "Yêu cầu đặt phòng đã được phê duyệt thành công.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Booking/Reject/5
        public async Task<IActionResult> Reject(int id)
        {
            var booking = await _bookingService.GetByIdAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            // Chỉ cho phép từ chối booking đang ở trạng thái chờ duyệt (1)
            if (booking.Status != 1)
            {
                TempData["ErrorMessage"] = "Chỉ có thể từ chối yêu cầu đang ở trạng thái chờ duyệt.";
                return RedirectToAction(nameof(Index));
            }

            return View(booking);
        }

        // POST: Booking/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectConfirmed(int id, string rejectReason)
        {
            var booking = await _bookingService.GetByIdAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            if (booking.Status != 1)
            {
                TempData["ErrorMessage"] = "Chỉ có thể từ chối yêu cầu đang ở trạng thái chờ duyệt.";
                return RedirectToAction(nameof(Index));
            }

            // Cập nhật trạng thái thành "Từ chối" (3) và lý do từ chối
            booking.Status = 3;
            booking.RejectReason = rejectReason;
            booking.UpdatedAt = DateTime.Now;
            await _bookingService.UpdateAsync(booking);

            // Lấy thông tin phòng và khung giờ cho thông báo
            var room = await _roomService.GetRoomByIdAsync(booking.RoomId);
            var timeSlotDto = await _timeSlotService.GetTimeSlotByIdAsync(booking.TimeSlotId);

            // Tạo thông báo cho người dùng
            var notification = new Notification
            {
                UserId = booking.UserId,
                Title = "Đặt phòng đã bị từ chối",
                Message = $"Yêu cầu đặt phòng {room.RoomName} vào ngày {booking.BookingDate.ToString("dd/MM/yyyy")}, khung giờ {timeSlotDto.StartTime}-{timeSlotDto.EndTime} đã bị từ chối. Lý do: {rejectReason}",
                IsRead = false,
                BookingId = booking.BookingId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            
            await _notificationService.CreateAsync(notification);
            
            // Gửi thông báo real-time cho user - thử cả hai cách
            try {
                // Cách 1: Sử dụng service
                await _signalRService.SendBookingStatusUpdateAsync(booking, notification.Message);
                
                // Cách 2: Gọi trực tiếp tới hub
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", notification.Message);
                await _hubContext.Clients.Group(booking.UserId.ToString())
                    .SendAsync("ReceiveBookingRejection", notification.Message, booking.BookingId);
                
                Console.WriteLine($"Đã gửi thông báo từ chối tới user {booking.UserId}: {notification.Message}");
            } catch (Exception ex) {
                Console.WriteLine($"Lỗi khi gửi thông báo: {ex.Message}");
            }

            TempData["SuccessMessage"] = "Yêu cầu đặt phòng đã bị từ chối.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Booking/Complete/5
        public async Task<IActionResult> Complete(int id)
        {
            var booking = await _bookingService.GetByIdAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            // Chỉ cho phép đánh dấu hoàn thành các booking đã được duyệt (2)
            if (booking.Status != 2)
            {
                TempData["ErrorMessage"] = "Chỉ có thể đánh dấu hoàn thành các yêu cầu đã được duyệt.";
                return RedirectToAction(nameof(Index));
            }

            return View(booking);
        }

        // POST: Booking/Complete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteConfirmed(int id)
        {
            var booking = await _bookingService.GetByIdAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            if (booking.Status != 2)
            {
                TempData["ErrorMessage"] = "Chỉ có thể đánh dấu hoàn thành các yêu cầu đã được duyệt.";
                return RedirectToAction(nameof(Index));
            }

            // Cập nhật trạng thái thành "Đã hoàn thành" (4)
            booking.Status = 4;
            booking.UpdatedAt = DateTime.Now;
            await _bookingService.UpdateAsync(booking);

            // Lấy thông tin phòng và khung giờ cho thông báo
            var room = await _roomService.GetRoomByIdAsync(booking.RoomId);
            var timeSlotDto = await _timeSlotService.GetTimeSlotByIdAsync(booking.TimeSlotId);

            // Tạo thông báo cho người dùng
            var notification = new Notification
            {
                UserId = booking.UserId,
                Title = "Đặt phòng đã hoàn thành",
                Message = $"Yêu cầu đặt phòng {room.RoomName} vào ngày {booking.BookingDate.ToString("dd/MM/yyyy")}, khung giờ {timeSlotDto.StartTime}-{timeSlotDto.EndTime} đã được đánh dấu hoàn thành.",
                IsRead = false,
                BookingId = booking.BookingId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            
            await _notificationService.CreateAsync(notification);
            
            // Gửi thông báo real-time cho user - thử cả hai cách
            try {
                // Cách 1: Sử dụng service
                await _signalRService.SendBookingStatusUpdateAsync(booking, notification.Message);
                
                // Cách 2: Gọi trực tiếp tới hub
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", notification.Message);
                await _hubContext.Clients.Group(booking.UserId.ToString())
                    .SendAsync("ReceiveBookingCompletion", notification.Message, booking.BookingId);
                
                Console.WriteLine($"Đã gửi thông báo hoàn thành tới user {booking.UserId}: {notification.Message}");
            } catch (Exception ex) {
                Console.WriteLine($"Lỗi khi gửi thông báo: {ex.Message}");
            }

            TempData["SuccessMessage"] = "Yêu cầu đặt phòng đã được đánh dấu hoàn thành.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Booking/Export
        public async Task<IActionResult> Export(string status = "all", string searchString = "", string dateFilter = "")
        {
            var bookings = await _bookingService.GetAllAsync();

            // Lọc theo trạng thái nếu có yêu cầu
            if (status != "all")
            {
                if (int.TryParse(status, out int statusInt))
                {
                    bookings = bookings.Where(b => b.Status == statusInt);
                }
            }

            // Lọc theo từ khóa tìm kiếm (phòng hoặc người dùng)
            if (!string.IsNullOrEmpty(searchString))
            {
                bookings = bookings.Where(b => 
                    b.Room.RoomName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                    b.User.FullName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                    b.User.Email.Contains(searchString, StringComparison.OrdinalIgnoreCase));
            }

            // Lọc theo ngày
            if (!string.IsNullOrEmpty(dateFilter) && DateOnly.TryParse(dateFilter, out DateOnly filterDate))
            {
                bookings = bookings.Where(b => b.BookingDate == filterDate);
            }

            // Sắp xếp theo ngày đặt, khung giờ
            bookings = bookings.OrderBy(b => b.BookingDate)
                                .ThenBy(b => b.TimeSlot.StartTime);

            // Tạo chuỗi CSV
            var csv = new System.Text.StringBuilder();
            
            // Tiêu đề cột
            csv.AppendLine("Mã đặt phòng,Người đặt,Email,Phòng,Tòa nhà,Ngày đặt,Khung giờ,Trạng thái,Lý do từ chối,Ngày tạo");
            
            // Dữ liệu
            foreach (var booking in bookings)
            {
                string status_text = GetStatusText(booking.Status);
                string reject_reason = booking.RejectReason ?? "";
                
                // Thay thế dấu phẩy trong các trường văn bản để tránh xảy ra lỗi CSV
                string roomName = booking.Room.RoomName.Replace(",", " ");
                string userName = booking.User.FullName.Replace(",", " ");
                string userEmail = booking.User.Email.Replace(",", " ");
                string building = (booking.Room.Building ?? "").Replace(",", " ");
                reject_reason = reject_reason.Replace(",", " ");
                
                // Lấy thông tin time slot
                var timeSlotDto = await _timeSlotService.GetTimeSlotByIdAsync(booking.TimeSlotId);
                var timeSlotInfo = $"{timeSlotDto.StartTime}-{timeSlotDto.EndTime}";
                
                csv.AppendLine($"{booking.BookingId},{userName},{userEmail},{roomName},{building},{booking.BookingDate:dd/MM/yyyy},{timeSlotInfo},{status_text},{reject_reason},{booking.CreatedAt:dd/MM/yyyy HH:mm}");
            }

            // Tạo tên file với thời gian hiện tại
            string fileName = $"danh-sach-dat-phong_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            
            // Trả về file CSV
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", fileName);
        }

        // Phương thức trợ giúp để lấy tên trạng thái theo mã số
        private string GetStatusText(int status)
        {
            return status switch
            {
                1 => "Chờ duyệt",
                2 => "Đã duyệt",
                3 => "Từ chối",
                4 => "Đã hoàn thành",
                5 => "Đã hủy",
                _ => status.ToString()
            };
        }
    }
}