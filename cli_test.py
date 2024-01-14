import shutil

history: list[str] = []

def main():
    terminal_size = shutil.get_terminal_size()
    while True:
        clear()
        for line in history[-terminal_size.lines:]:
            print(line)
        # Check for arrow key presses
        user_input = input(">>> ")
        history.append(user_input)

def clear():
    print(chr(27) + "[2J")

if __name__ == "__main__":
    main()