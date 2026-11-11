import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-welcome',
  imports: [RouterLink, BrandLogo],
  templateUrl: './welcome.page.html',
  styleUrl: './welcome.page.scss',
})
export class WelcomePage {}
