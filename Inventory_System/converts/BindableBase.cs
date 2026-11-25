using DevExpress.Mvvm;
using Inventory_System.dbContext;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Inventory_System.converts
{
    public class EditIssueInfo : BindableBase
    {
        public EditIssueInfo(Cont_maintContext dbContext, IList users)
        {
            DbContext = dbContext;
            Users = users;
        }
        public Cont_maintContext DbContext { get; }
        public IList Users { get; }
    }

}
