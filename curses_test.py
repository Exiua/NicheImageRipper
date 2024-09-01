import curses

def main(stdscr):
    stdscr.addstr("Hello, Curses!")
    stdscr.refresh()
    stdscr.getkey()

curses.wrapper(main)