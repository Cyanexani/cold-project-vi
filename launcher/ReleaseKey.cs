// edupode's release key, public half: the launcher installs an update only when manifest.json is
// signed with the private half (scripts\new-release-key.ps1 made it; it never leaves edupode's PC).
namespace VIctoryLauncher
{
    static class ReleaseKey
    {
        public const string PublicXml = "<RSAKeyValue><Modulus>42dDIOMzxtofDge6A4ZfY66fmb/k5TYrK+s4RkvWwlrdudsU81xADauQe+xn6QaebyNCXG4NM1e26yHy/Jdacb7y3ZReqkx4Sh7qX9Hui4rSyxUKG2tJsYCfOFPOMK1QgbUfnqQ6vJJLykWAQUbEVOtZxW89CVjUBQUzspjE3/j/p6WyDYO+PpN6OieIjpgQ7rDQSJ9lSe0krDZHYWqhqfsaL/2kGZPeERrBnaa7XrAgxru9/8VAZ5fOUorXY346N82vnb+Jv8sxeL/FYwCPWd4A38cMOTufN68GFnORIMBuxMYDTnVJ4qtc6erWqARdcVm5yrNHQM5YzeUlLEXY92gLAYa7RXoYXo7Ta459uhxzeT9shG+gX4FQK+6+ObOj0a4FSNCTFIa+XmTN1M/FHrDVwzDaO5YAtd0Et4tktFnm4F21Szg2rjOE7R2l+nK4fAkA4Ik0S0BGff5HTMip9whwucbJCiIV+Nmfy1izWM1yFhK3y3ifcSS/Z+YZMBiB</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
    }
}