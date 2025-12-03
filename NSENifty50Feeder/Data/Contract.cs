using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NSENifty50Feeder.Data
{
    public class Contract 
    {
        private string _symbol;
        private string _expiry;
        private string _optionType; // CE or PE
        private double _strike;


        private int myVar;

        public string Symbol
        {
            get { return _symbol; }
            set { _symbol = value; }
        }
        public string Expiry
        {
            get { return _expiry; }
            set { _expiry = value; }
        }
        public string OptionType
        {
            get { return _optionType; }
            set { _optionType = value; }
        }

        public double Strike
        {
            get { return _strike; }
            set { _strike = value; }
        }





 


        public int Token { get; internal set; }
      
    }
}
