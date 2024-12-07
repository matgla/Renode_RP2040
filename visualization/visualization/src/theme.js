import { red } from '@mui/material/colors';
import { createTheme } from '@mui/material/styles';

// A custom theme for this app
const theme = createTheme({
  cssVariables: true,
  palette: {
    primary: {
      main: '#414fc3',
    },
    secondary: {
      main: '#f50057',
    },
    background: {
      default: '#eeeeee',
    },
  },
});

export default theme;