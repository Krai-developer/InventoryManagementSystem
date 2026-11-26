using System;

namespace InventoryManagementSystem.Models
{
    public class Supplier
    {
        private int _supplierId;
        private string _supplierName = string.Empty;
        private string _contactPerson = string.Empty;
        private string _phoneNumber = string.Empty;
        private string _emailAddress = string.Empty;
        private string _businessAddress = string.Empty;
        private int _productCount;

        public int SupplierID
        {
            get => _supplierId;
            set => _supplierId = value >= 0 ? value : 0;
        }

        public string SupplierName
        {
            get => _supplierName;
            set => _supplierName = (value ?? string.Empty).Trim();
        }

        public string ContactPerson
        {
            get => _contactPerson;
            set => _contactPerson = (value ?? string.Empty).Trim();
        }

        public string PhoneNumber
        {
            get => _phoneNumber;
            set => _phoneNumber = (value ?? string.Empty).Trim();
        }

        public string EmailAddress
        {
            get => _emailAddress;
            set => _emailAddress = (value ?? string.Empty).Trim();
        }

        public string BusinessAddress
        {
            get => _businessAddress;
            set => _businessAddress = (value ?? string.Empty).Trim();
        }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int ProductCount
        {
            get => _productCount;
            set => _productCount = value >= 0 ? value : 0;
        }

        public Supplier()
        {
        }

        public Supplier(
            int supplierId,
            string supplierName,
            string contactPerson = "",
            string phoneNumber = "",
            string emailAddress = "",
            string businessAddress = "")
        {
            SupplierID = supplierId;
            SupplierName = supplierName;
            ContactPerson = contactPerson;
            PhoneNumber = phoneNumber;
            EmailAddress = emailAddress;
            BusinessAddress = businessAddress;
            CreatedAt = DateTime.Now;
        }

        public bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(SupplierName))
            {
                errorMessage = "Supplier Name cannot be empty.";
                return false;
            }

            if (SupplierName.Length > 150)
            {
                errorMessage = "Supplier Name cannot exceed 150 characters.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(EmailAddress) && !EmailAddress.Contains('@'))
            {
                errorMessage = "Please enter a valid Email Address.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public override string ToString()
        {
            return SupplierName;
        }
    }
}
