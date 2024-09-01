from __future__ import annotations

import json
from datetime import datetime, timedelta
from pathlib import Path

import requests

class Token:
    def __init__(self, value: str, expiry: datetime):
        self.value: str = value
        self.expiration: datetime = expiry

    def __str__(self) -> str:
        return self.value

    def serialize(self) -> dict[str, str]:
        return {
            "value": self.value,
            "expiration": self.expiration.isoformat()
        }
    
    @classmethod
    def deserialize(cls, data: dict[str, str]) -> Token:
        return cls(data["value"], datetime.fromisoformat(data["expiration"]))

class TokenManager:
    __instance: TokenManager = None

    def __init__(self):
        self.tokens: dict[str, Token] = {}
        self.load_tokens()
    
    def load_tokens(self):
        path = Path("temp_tokens.json")
        if path.exists():
            with path.open("r") as f:
                data: dict = json.load(f)
            self.tokens = {key: Token.deserialize(value) for key, value in data.items()}
    
    def save_tokens(self):
        path = Path("temp_tokens.json")
        with path.open("w") as f:
            json.dump({key: token.serialize() for key, token in self.tokens.items()}, f)
    
    @staticmethod
    def get_instance() -> TokenManager:
        if TokenManager.__instance is None:
            TokenManager.__instance = TokenManager()
        return TokenManager.__instance
    
    def get_token(self, key: str) -> Token:
        token = self.tokens.get(key)
        if token is None or token.expiration < datetime.now():
            token = self.generate_token(key)
            self.tokens[key] = token
            self.save_tokens()
        return token
    
    def generate_token(self, key: str) -> Token:
        match key:
            case "redgifs":
                return self.__generate_redgifs_token()
            case _:
                raise ValueError(f"Unknown token key: {key}")
    
    def __generate_redgifs_token(self) -> Token:
        response = requests.get("http://api.redgifs.com/v2/auth/temporary")
        json_ = response.json()
        expiration = datetime.now() + timedelta(hours=24)
        return Token(json_["token"], expiration)

if __name__ == "__main__":
    pass