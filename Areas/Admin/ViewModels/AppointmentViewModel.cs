using Microsoft.AspNetCore.Mvc.Rendering;
using PetGroomingAppointmentSystem.Models;
using System;
using System.Collections.Generic;

namespace PetGroomingAppointmentSystem.Models.ViewModels
{
    public class AppointmentViewModel
    {
        // 用于在表格中显示预约列表
        public IEnumerable<Appointment> Appointments { get; set; }

        // 用于筛选功能的下拉列表
        public IEnumerable<SelectListItem> StaffList { get; set; }
        public IEnumerable<SelectListItem> CustomerList { get; set; }
        public IEnumerable<SelectListItem> PetList { get; set; }

        // 用于创建/编辑表单的属性
        public string? AppointmentId { get; set; }
        public string? CustomerId { get; set; }
        public List<string> PetId { get; set; }
        public Dictionary<string, string> PetServiceMap { get; set; } // Changed from ServiceId to match form submission
        public string GroomerMode { get; set; } // 'any', 'one', 'individual'
        public Dictionary<string, string> PetGroomerMap { get; set; } // For individual groomer assignment
        public DateTime AppointmentDateTime { get; set; }
        public string? StaffId { get; set; }
        public string? ServiceId { get; set; } // For single appointment edit
        public string? EditAppointmentId { get; set; } // For single appointment edit

        public string? SpecialRequest { get; set; }
        public string? Status { get; set; }

        // 用于筛选的属性
        public string? FilterStatus { get; set; }
        public string? FilterGroomerId { get; set; }
        public DateTime? FilterDate { get; set; }
        public string? FilterAppointmentId { get; set; } // 新增：用于筛选预约ID
        public string? FilterCustomerName { get; set; } // 新增：用于筛选顾客姓名

        public AppointmentViewModel()
        {
            Appointments = new List<Appointment>();
            StaffList = new List<SelectListItem>();
            CustomerList = new List<SelectListItem>();
            PetList = new List<SelectListItem>();
            PetId = new List<string>();
            PetServiceMap = new Dictionary<string, string>(); // Initialize the dictionary
            PetGroomerMap = new Dictionary<string, string>();
        }
    }
}
