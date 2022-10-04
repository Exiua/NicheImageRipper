class RipperError(Exception):
    """General Ripper Exceptions"""
    pass


class WrongExtension(RipperError):
    """File not found due to using incorrect extension"""
    pass


class InvalidSubdomain(RipperError):
    """Url does not have a supported subdomain"""
    pass


class BadSubdomain(RipperError):
    """Incorrect .party subdomain used"""
    pass


class FileNotFoundAtUrl(RipperError):
    """File was not found at url"""
    pass
